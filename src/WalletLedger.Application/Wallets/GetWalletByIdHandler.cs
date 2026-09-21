using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Dtos;
using WalletLedger.Application.Mapping;

namespace WalletLedger.Application.Wallets;

public sealed record GetWalletByIdQuery(Guid OwnerUserId, Guid WalletId, bool CallerIsAdmin = false);

public sealed class GetWalletByIdHandler(IWalletRepository walletRepository)
{
    /// <summary>
    /// Returns null both when the wallet doesn't exist and when it belongs to someone else —
    /// the API layer maps both to 404, so a non-owner can never distinguish "not found" from
    /// "not yours" (no 403 here; that would leak existence). An admin caller bypasses the
    /// ownership check entirely (see ARCHITECTURE.md AuthN/AuthZ) - admins are a trusted
    /// internal role, not subject to the anti-enumeration protection non-owners get.
    /// </summary>
    public async Task<WalletDto?> HandleAsync(GetWalletByIdQuery query, CancellationToken ct)
    {
        var account = await walletRepository.GetByIdAsync(query.WalletId, ct);
        if (account is null)
        {
            return null;
        }

        if (account.OwnerUserId != query.OwnerUserId && !query.CallerIsAdmin)
        {
            return null;
        }

        return account.ToDto();
    }
}
