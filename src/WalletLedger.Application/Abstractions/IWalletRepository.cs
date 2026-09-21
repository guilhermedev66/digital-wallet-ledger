using WalletLedger.Domain.Entities;

namespace WalletLedger.Application.Abstractions;

public interface IWalletRepository
{
    Task AddAsync(LedgerAccount account, CancellationToken ct);

    Task<LedgerAccount?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<LedgerAccount>> ListByOwnerAsync(Guid ownerUserId, CancellationToken ct);
}
