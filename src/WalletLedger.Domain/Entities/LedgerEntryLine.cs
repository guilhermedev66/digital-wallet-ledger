using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Domain.Entities;

/// <summary>An entry not yet posted - the input shape Transaction.Post validates and turns into a real LedgerEntry.</summary>
public sealed record LedgerEntryLine(Guid AccountId, LedgerEntryDirection Direction, long AmountMinorUnits, Currency Currency);
