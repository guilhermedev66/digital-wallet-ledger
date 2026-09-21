using WalletLedger.Application.Dtos;
using WalletLedger.Domain.Entities;

namespace WalletLedger.Application.Mapping;

public static class TransactionMapping
{
    public static TransactionDto ToDto(this Transaction transaction) => new(
        transaction.Id,
        transaction.Type.ToString(),
        transaction.PostedAtUtc,
        transaction.Entries
            .Select(e => new LedgerEntryDto(e.AccountId, e.Direction.ToString(), e.AmountMinorUnits, e.Currency.ToString()))
            .ToList());
}
