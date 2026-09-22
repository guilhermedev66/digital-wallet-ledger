namespace WalletLedger.Application.Dtos;

public sealed record LedgerEntryDto(Guid AccountId, string Direction, long AmountMinorUnits, string Currency);

public sealed record TransactionDto(Guid Id, string Type, DateTime PostedAtUtc, Guid? ReversalOfTransactionId, IReadOnlyList<LedgerEntryDto> Entries);
