using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WalletLedger.Application.Abstractions;
using WalletLedger.Domain.Entities;

namespace WalletLedger.Infrastructure.Security;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private const int MinimumSigningKeyBytes = 32;

    private readonly JwtOptions _options;
    private readonly SigningCredentials _signingCredentials;

    public JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        _options = options.Value;

        var keyBytes = Encoding.UTF8.GetBytes(_options.SigningKey);
        if (keyBytes.Length < MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"JWT signing key must be at least {MinimumSigningKeyBytes} bytes (256 bits) for HS256; " +
                $"configured key is only {keyBytes.Length} bytes. Set a longer value for Jwt:SigningKey / JWT__SIGNINGKEY.");
        }

        _signingCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
    }

    public AuthToken GenerateToken(User user)
    {
        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddMinutes(_options.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email.Value),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (user.IsAdmin)
        {
            claims.Add(new Claim("role", "admin"));
        }

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAtUtc,
            signingCredentials: _signingCredentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new AuthToken(accessToken, expiresAtUtc);
    }
}
