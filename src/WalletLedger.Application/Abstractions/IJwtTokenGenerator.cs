using WalletLedger.Domain.Entities;

namespace WalletLedger.Application.Abstractions;

public interface IJwtTokenGenerator
{
    AuthToken GenerateToken(User user);
}

public sealed record AuthToken(string AccessToken, DateTime ExpiresAtUtc);
