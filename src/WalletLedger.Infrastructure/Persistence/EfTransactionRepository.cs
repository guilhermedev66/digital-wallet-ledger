using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Exceptions;
using WalletLedger.Domain.Entities;
using WalletLedger.Infrastructure.Persistence.Configurations;

namespace WalletLedger.Infrastructure.Persistence;

public sealed class EfTransactionRepository(WalletLedgerDbContext dbContext) : ITransactionRepository
{
    public async Task AddAsync(Transaction transaction, CancellationToken ct)
    {
        // A single SaveChangesAsync call already wraps the Transaction row and its cascading
        // LedgerEntry rows in one implicit DB transaction (EF Core's default behavior for any
        // one call, on a provider that supports transactions) - no explicit BeginTransaction
        // needed here, unlike PostTransferIfSufficientFundsAsync below, which spans multiple
        // separate statements (lock, balance read, insert) that must share one transaction.
        dbContext.Transactions.Add(transaction);

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, TransactionConfiguration.IdempotencyKeyIndexName))
        {
            throw new IdempotencyKeyAlreadyUsedException();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, TransactionConfiguration.ReversalOfTransactionIdIndexName))
        {
            throw new TransactionAlreadyReversedException();
        }
    }

    public async Task<long> GetAccountBalanceAsync(Guid accountId, CancellationToken ct)
    {
        // One SQL statement (a single SUM over a signed CASE expression), not two separate
        // debit/credit sums - two round trips would leave a torn-read window where a
        // concurrent post between them could be reflected in one sum but not the other.
        return await dbContext.LedgerEntries
            .Where(e => e.AccountId == accountId)
            .SumAsync(e => (long?)(e.Direction == LedgerEntryDirection.Debit ? e.AmountMinorUnits : -e.AmountMinorUnits), ct)
            ?? 0;
    }

    public Task<Transaction?> FindByIdempotencyKeyAsync(Guid requestedByUserId, string idempotencyKey, CancellationToken ct) =>
        dbContext.Transactions
            .Include(t => t.Entries)
            .SingleOrDefaultAsync(t => t.RequestedByUserId == requestedByUserId && t.IdempotencyKey == idempotencyKey, ct);

    public Task<Transaction?> GetByIdAsync(Guid transactionId, CancellationToken ct) =>
        dbContext.Transactions
            .Include(t => t.Entries)
            .SingleOrDefaultAsync(t => t.Id == transactionId, ct);

    public Task<Transaction?> FindReversalOfAsync(Guid originalTransactionId, CancellationToken ct) =>
        dbContext.Transactions
            .Include(t => t.Entries)
            .SingleOrDefaultAsync(t => t.ReversalOfTransactionId == originalTransactionId, ct);

    public async Task<TransferPostResult> PostTransferIfSufficientFundsAsync(Guid sourceAccountId, long debitAmount, Transaction transaction, CancellationToken ct)
    {
        // ReadCommitted (Postgres's own default) is enough - the row lock below is what
        // provides the serialization point for the balance check, not the isolation level.
        // Deliberately not SERIALIZABLE: that would add spurious serialization-failure retries
        // unrelated to the one row this is actually protecting, for no extra safety here.
        await using var dbTransaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        // Row lock on the source account only - concurrent transfers debiting THIS account
        // serialize on this lock (the second waits until the first commits/rolls back, then
        // sees its committed entries when it re-reads the balance below). Nothing locks the
        // destination account: a credit can never overdraw, so there is nothing on that side
        // that needs protecting, and never taking two locks per transfer means this can't
        // deadlock against another transfer doing the reverse A<->B direction at the same time.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM "LedgerAccounts" WHERE "Id" = {sourceAccountId} FOR UPDATE""", ct);

        // Idempotency check happens here - under the lock, before the balance check - not
        // just as a side effect of the unique-constraint violation on insert below. If the
        // balance check ran first: two concurrent identical requests (same key) against a
        // near-exhausted balance would let the winner's commit push the balance below the
        // transfer amount before the loser re-reads it, so the loser would see
        // InsufficientFunds for what was actually an already-successful replay of its own
        // exact request - a client retrying on that false failure with a NEW key would
        // genuinely double-transfer. Found in review (M3 revalidation) - the existing
        // concurrent-replay test funded well above the debit amount and never exercised the
        // near-limit case where this branch actually matters.
        var alreadyPosted = await dbContext.Transactions
            .AnyAsync(t => t.RequestedByUserId == transaction.RequestedByUserId && t.IdempotencyKey == transaction.IdempotencyKey, ct);

        if (alreadyPosted)
        {
            await dbTransaction.RollbackAsync(ct);
            throw new IdempotencyKeyAlreadyUsedException();
        }

        // Same ordering lesson as the idempotency check above (see MEMORY.md, M3 financial-
        // correctness note): when this posting `transaction` is a reversal, check "has the
        // original already been reversed by someone else" here, under the lock, before the
        // balance check - not after. Otherwise two concurrent reversal attempts for the same
        // original transaction (different idempotency keys) would let the balance check on the
        // second one run against a balance the first one's commit already reduced, misreporting
        // InsufficientFunds for what should be TransactionAlreadyReversedException.
        if (transaction.ReversalOfTransactionId is Guid originalTransactionId)
        {
            var alreadyReversed = await dbContext.Transactions
                .AnyAsync(t => t.ReversalOfTransactionId == originalTransactionId, ct);

            if (alreadyReversed)
            {
                await dbTransaction.RollbackAsync(ct);
                throw new TransactionAlreadyReversedException();
            }
        }

        var currentBalance = await GetAccountBalanceAsync(sourceAccountId, ct);

        if (currentBalance < debitAmount)
        {
            await dbTransaction.RollbackAsync(ct);
            return new TransferPostResult(TransferPostOutcome.InsufficientFunds, null);
        }

        dbContext.Transactions.Add(transaction);

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, TransactionConfiguration.IdempotencyKeyIndexName))
        {
            await dbTransaction.RollbackAsync(ct);
            throw new IdempotencyKeyAlreadyUsedException();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, TransactionConfiguration.ReversalOfTransactionIdIndexName))
        {
            await dbTransaction.RollbackAsync(ct);
            throw new TransactionAlreadyReversedException();
        }

        await dbTransaction.CommitAsync(ct);

        return new TransferPostResult(TransferPostOutcome.Posted, transaction);
    }

    public async Task<(IReadOnlyList<Transaction> Items, int TotalCount)> ListTransactionsForAccountAsync(
        Guid accountId, int page, int pageSize, DateTime? fromUtc, DateTime? toUtc, TransactionType? type, CancellationToken ct)
    {
        var query = dbContext.Transactions.Where(t => t.Entries.Any(e => e.AccountId == accountId));

        if (fromUtc is not null)
        {
            query = query.Where(t => t.PostedAtUtc >= fromUtc);
        }

        if (toUtc is not null)
        {
            query = query.Where(t => t.PostedAtUtc <= toUtc);
        }

        if (type is not null)
        {
            query = query.Where(t => t.Type == type);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Include(t => t.Entries)
            .OrderByDescending(t => t.PostedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public Task<IReadOnlyList<LedgerEntryProjection>> ListEntriesForTransactionsTouchingAccountAsync(Guid accountId, CancellationToken ct)
    {
        var transactionIds = dbContext.LedgerEntries.Where(e => e.AccountId == accountId).Select(e => e.TransactionId);

        return ProjectEntriesAsync(dbContext.LedgerEntries.Where(e => transactionIds.Contains(e.TransactionId)), ct);
    }

    public Task<IReadOnlyList<LedgerEntryProjection>> ListAllEntriesAsync(CancellationToken ct) =>
        ProjectEntriesAsync(dbContext.LedgerEntries, ct);

    private static async Task<IReadOnlyList<LedgerEntryProjection>> ProjectEntriesAsync(IQueryable<LedgerEntry> query, CancellationToken ct) =>
        await query
            .Select(e => new LedgerEntryProjection(e.TransactionId, e.AccountId, e.Direction, e.AmountMinorUnits, e.Currency))
            .ToListAsync(ct);

    private static bool IsUniqueViolation(DbUpdateException ex, string indexName) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
        && pg.ConstraintName == indexName;
}
