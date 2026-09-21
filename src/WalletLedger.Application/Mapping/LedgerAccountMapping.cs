using WalletLedger.Application.Dtos;
using WalletLedger.Domain.Entities;

namespace WalletLedger.Application.Mapping;

public static class LedgerAccountMapping
{
    // WalletDto is the user-facing wallet surface only - a SystemFunding account (null
    // OwnerUserId) should never reach this mapping; treat it as a programming error if it does.
    public static WalletDto ToDto(this LedgerAccount account) =>
        new(
            account.Id,
            account.OwnerUserId ?? throw new InvalidOperationException("Cannot map a system account to a WalletDto."),
            account.Currency.ToString(),
            account.DisplayName,
            account.CreatedAtUtc);
}
