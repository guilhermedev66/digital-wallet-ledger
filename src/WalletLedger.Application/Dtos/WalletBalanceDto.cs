namespace WalletLedger.Application.Dtos;

public sealed record WalletBalanceDto(Guid WalletId, string Currency, long BalanceMinorUnits);
