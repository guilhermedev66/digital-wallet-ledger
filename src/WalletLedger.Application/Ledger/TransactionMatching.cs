using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Ledger;

/// <summary>Shared by every handler that needs idempotent-replay matching: "is this already-posted transaction the same logical request, or a key reused for something different?"</summary>
internal static class TransactionMatching
{
    public static bool HasMatchingEntry(this Transaction transaction, Guid accountId, LedgerEntryDirection direction, long amountMinorUnits, Currency currency) =>
        transaction.Entries.Any(e =>
            e.AccountId == accountId && e.Direction == direction && e.AmountMinorUnits == amountMinorUnits && e.Currency == currency);
}
