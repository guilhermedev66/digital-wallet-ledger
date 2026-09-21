using Microsoft.AspNetCore.Identity;
using WalletLedger.Application.Abstractions;
using WalletLedger.Domain.Entities;

namespace WalletLedger.Infrastructure.Security;

/// <summary>Wraps ASP.NET Core Identity's vetted PBKDF2-HMAC-SHA256 hasher — no hand-rolled crypto.</summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    public string Hash(string password) => _inner.HashPassword(user: null!, password);

    public bool Verify(string password, string passwordHash)
    {
        var result = _inner.VerifyHashedPassword(user: null!, hashedPassword: passwordHash, providedPassword: password);
        return result != PasswordVerificationResult.Failed;
    }
}
