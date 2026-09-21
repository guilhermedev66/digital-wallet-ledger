using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Dtos;
using WalletLedger.Application.Mapping;

namespace WalletLedger.Application.Wallets;

public sealed record GetWalletByIdQuery(Guid OwnerUserId, Guid WalletId);

public sealed class GetWalletByIdHandler(IWalletRepository walletRepository)
{
    /// <summary>
    /// Returns null both when the wallet doesn't exist and when it belongs to someone else —
    /// the API layer maps both to 404, so a non-owner can never distinguish "not found" from
    /// "not yours" (no 403 here; that would leak existence).
    /// </summary>
    public async Task<WalletDto?> HandleAsync(GetWalletByIdQuery query, CancellationToken ct)
    {
        var account = await walletRepository.GetByIdAsync(query.WalletId, ct);
        if (account is null || account.OwnerUserId != query.OwnerUserId)
        {
            return null;
        }

        return account.ToDto();
    }
}
