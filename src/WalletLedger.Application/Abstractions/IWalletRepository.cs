using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Abstractions;

public interface IWalletRepository
{
    Task AddAsync(LedgerAccount account, CancellationToken ct);

    Task<LedgerAccount?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<LedgerAccount>> ListByOwnerAsync(Guid ownerUserId, CancellationToken ct);

    /// <summary>The one SystemFunding account for a currency - seeded out-of-band at startup, never created via a client-facing endpoint.</summary>
    Task<LedgerAccount?> GetSystemFundingAccountAsync(Currency currency, CancellationToken ct);

    /// <summary>Every LedgerAccount in the system (wallets and SystemFunding alike). Global reconciliation only - admin-gated at the API boundary, see ReconciliationController.</summary>
    Task<IReadOnlyList<LedgerAccount>> ListAllAsync(CancellationToken ct);
}
