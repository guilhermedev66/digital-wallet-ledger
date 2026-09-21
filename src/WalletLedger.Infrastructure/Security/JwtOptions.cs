namespace WalletLedger.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string SigningKey { get; set; }

    public required string Issuer { get; set; }

    public required string Audience { get; set; }

    public int ExpiryMinutes { get; set; } = 60;
}
