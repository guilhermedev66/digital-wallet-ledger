using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Exceptions;
using WalletLedger.Domain.Entities;

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
        catch (DbUpdateException ex) when (IsUniqueIdempotencyViolation(ex))
        {
            throw new IdempotencyKeyAlreadyUsedException();
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
        catch (DbUpdateException ex) when (IsUniqueIdempotencyViolation(ex))
        {
            await dbTransaction.RollbackAsync(ct);
            throw new IdempotencyKeyAlreadyUsedException();
        }

        await dbTransaction.CommitAsync(ct);

        return new TransferPostResult(TransferPostOutcome.Posted, transaction);
    }

    private static bool IsUniqueIdempotencyViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
