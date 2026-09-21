using WalletLedger.Domain.Entities;

namespace WalletLedger.Application.Abstractions;

public enum TransferPostOutcome
{
    Posted,
    InsufficientFunds,
}

public sealed record TransferPostResult(TransferPostOutcome Outcome, Transaction? Transaction);

public interface ITransactionRepository
{
    /// <summary>
    /// For posts with no debit-ceiling to enforce (e.g. SimulatedFunding - the system funding
    /// account isn't balance-checked). Throws IdempotencyKeyAlreadyUsedException if the DB's
    /// (RequestedByUserId, IdempotencyKey) unique constraint is violated.
    /// </summary>
    Task AddAsync(Transaction transaction, CancellationToken ct);

    /// <summary>
    /// Debit entries minus credit entries for the account, per ARCHITECTURE.md's balance
    /// model - every LedgerAccount uses this one uniform sign convention (debit-normal, like
    /// an asset account): a SystemFunding account's own "balance" is expected to go negative
    /// over time since it only ever gives money away, and that's fine - it's never surfaced
    /// to a caller. Sums directly from LedgerEntry rows, never a cached/mutable column, as one
    /// single SQL statement (never two separate debit/credit sums - see EfTransactionRepository
    /// for why that would be a torn read).
    /// </summary>
    Task<long> GetAccountBalanceAsync(Guid accountId, CancellationToken ct);

    Task<Transaction?> FindByIdempotencyKeyAsync(Guid requestedByUserId, string idempotencyKey, CancellationToken ct);

    /// <summary>
    /// Locks sourceAccountId's row for the duration of one DB transaction (SELECT ... FOR
    /// UPDATE - see ARCHITECTURE.md's concurrency section), re-derives its current balance
    /// under that lock, and only posts `transaction` if the balance covers `debitAmount`.
    /// Concurrent transfers debiting the SAME account serialize on this lock; nothing here
    /// locks the destination account (credits can't overdraw, so nothing needs protecting on
    /// that side - see MEMORY.md for why that's sufficient and doesn't risk a lock-ordering
    /// deadlock). Throws IdempotencyKeyAlreadyUsedException on the same unique-constraint race
    /// as AddAsync.
    /// </summary>
    Task<TransferPostResult> PostTransferIfSufficientFundsAsync(Guid sourceAccountId, long debitAmount, Transaction transaction, CancellationToken ct);

    /// <summary>
    /// Transactions with at least one entry against accountId, most recent first, paginated.
    /// TotalCount is the total matching row count (for computing total pages), not the page size.
    /// </summary>
    Task<(IReadOnlyList<Transaction> Items, int TotalCount)> ListTransactionsForAccountAsync(Guid accountId, int page, int pageSize, CancellationToken ct);
}
