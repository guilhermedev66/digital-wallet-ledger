using WalletLedger.Domain.Entities;

namespace WalletLedger.Application.Abstractions;

public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction, CancellationToken ct);

    /// <summary>
    /// Debit entries minus credit entries for the account, per ARCHITECTURE.md's balance
    /// model - every LedgerAccount uses this one uniform sign convention (debit-normal, like
    /// an asset account): a SystemFunding account's own "balance" is expected to go negative
    /// over time since it only ever gives money away, and that's fine - it's never surfaced
    /// to a caller. Sums directly from LedgerEntry rows, never a cached/mutable column.
    /// </summary>
    Task<long> GetAccountBalanceAsync(Guid accountId, CancellationToken ct);
}
