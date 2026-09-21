using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Dtos;
using WalletLedger.Application.Mapping;

namespace WalletLedger.Application.Wallets;

public sealed record ListMyWalletsQuery(Guid OwnerUserId);

public sealed class ListMyWalletsHandler(IWalletRepository walletRepository)
{
    public async Task<IReadOnlyList<WalletDto>> HandleAsync(ListMyWalletsQuery query, CancellationToken ct)
    {
        var accounts = await walletRepository.ListByOwnerAsync(query.OwnerUserId, ct);
        return accounts.Select(a => a.ToDto()).ToList();
    }
}
