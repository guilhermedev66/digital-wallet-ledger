namespace WalletLedger.Application.Dtos;

public sealed record WalletDto(Guid Id, Guid OwnerUserId, string Currency, string? DisplayName, DateTime CreatedAtUtc);
