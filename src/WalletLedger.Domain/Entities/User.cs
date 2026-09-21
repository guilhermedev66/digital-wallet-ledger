using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public bool IsAdmin { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private User()
    {
    }

    public static User Register(Email email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash cannot be empty.", nameof(passwordHash));
        }

        return new User
        {
            Id = Guid.NewGuid(),
            Email = email ?? throw new ArgumentNullException(nameof(email)),
            PasswordHash = passwordHash,
            IsAdmin = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// No self-service path grants this - there is no registration flag or API for it.
    /// Admin status is elevated out-of-band (direct data change) and is a deliberately
    /// heavy-handed, rare operation, not a role a user can request for themselves.
    /// </summary>
    public void PromoteToAdmin() => IsAdmin = true;
}
