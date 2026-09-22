using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Abstractions;

public enum TransferPostOutcome
{
    Posted,
    InsufficientFunds,
}

public sealed record TransferPostResult(TransferPostOutcome Outcome, Transaction? Transaction);

/// <summary>
/// A raw LedgerEntry row, projected independently of the Transaction aggregate. Reconciliation
/// (see ReconciliationEngine) is deliberately built on this instead of loaded Transaction/Entries
/// navigations: the point of reconciliation is to re-derive the ledger's truth straight from
/// LedgerEntry rows, the same way a defense-in-depth check against DB tampering or a future
/// bug would have to - not to trust that whatever produced a Transaction object already got it
/// right. It also happens to make the detection logic trivially unit-testable, since
/// Transaction.Post structurally can't be used to construct an unbalanced Transaction on purpose.
/// </summary>
public sealed record LedgerEntryProjection(Guid TransactionId, Guid AccountId, LedgerEntryDirection Direction, long AmountMinorUnits, Currency Currency);

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

    /// <summary>By primary key, entries included. Used by ReverseTransactionHandler to load the transaction being reversed.</summary>
    Task<Transaction?> GetByIdAsync(Guid transactionId, CancellationToken ct);

    /// <summary>The existing reversal of originalTransactionId, if any (a transaction can only ever be reversed once - see TransactionAlreadyReversedException).</summary>
    Task<Transaction?> FindReversalOfAsync(Guid originalTransactionId, CancellationToken ct);

    /// <summary>
    /// Locks sourceAccountId's row for the duration of one DB transaction (SELECT ... FOR
    /// UPDATE - see ARCHITECTURE.md's concurrency section), re-derives its current balance
    /// under that lock, and only posts `transaction` if the balance covers `debitAmount`.
    /// Concurrent transfers debiting the SAME account serialize on this lock; nothing here
    /// locks the destination account (credits can't overdraw, so nothing needs protecting on
    /// that side - see MEMORY.md for why that's sufficient and doesn't risk a lock-ordering
    /// deadlock). Throws IdempotencyKeyAlreadyUsedException on the same unique-constraint race
    /// as AddAsync. Also used by ReverseTransactionHandler to reverse a Transfer (the account
    /// being newly debited by the reversal is the original transfer's destination wallet, which
    /// genuinely can be overdrawn if it's since been spent elsewhere) - so also throws
    /// TransactionAlreadyReversedException on the reversal-uniqueness race.
    /// </summary>
    Task<TransferPostResult> PostTransferIfSufficientFundsAsync(Guid sourceAccountId, long debitAmount, Transaction transaction, CancellationToken ct);

    /// <summary>
    /// Transactions with at least one entry against accountId, most recent first, paginated.
    /// TotalCount is the total matching row count (for computing total pages), not the page
    /// size. fromUtc/toUtc/type filter on PostedAtUtc (inclusive) and Type when given.
    /// </summary>
    Task<(IReadOnlyList<Transaction> Items, int TotalCount)> ListTransactionsForAccountAsync(
        Guid accountId, int page, int pageSize, DateTime? fromUtc, DateTime? toUtc, TransactionType? type, CancellationToken ct);

    /// <summary>
    /// Raw entries for every transaction that has at least one entry against accountId - i.e.
    /// every entry of every transaction the account is a party to, not just that account's own
    /// entries, since reconciliation needs a transaction's WHOLE entry set to verify its
    /// debit/credit balance (see ReconciliationEngine).
    /// </summary>
    Task<IReadOnlyList<LedgerEntryProjection>> ListEntriesForTransactionsTouchingAccountAsync(Guid accountId, CancellationToken ct);

    /// <summary>Every LedgerEntry row in the system. Global reconciliation only - admin-gated at the API boundary, see ReconciliationController.</summary>
    Task<IReadOnlyList<LedgerEntryProjection>> ListAllEntriesAsync(CancellationToken ct);
}
