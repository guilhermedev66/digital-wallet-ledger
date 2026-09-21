using WalletLedger.Application.Dtos;
using WalletLedger.Domain.Entities;

namespace WalletLedger.Application.Mapping;

public static class LedgerAccountMapping
{
    public static WalletDto ToDto(this LedgerAccount account) =>
        new(account.Id, account.OwnerUserId, account.Currency.ToString(), account.DisplayName, account.CreatedAtUtc);
}
