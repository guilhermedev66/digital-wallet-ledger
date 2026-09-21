namespace WalletLedger.Application.Dtos;

public sealed record AuthResultDto(Guid UserId, string Email, string AccessToken, DateTime ExpiresAtUtc);
