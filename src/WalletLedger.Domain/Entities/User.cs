using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
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
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    /// <summary>Rehydration path for persistence mapping — bypasses the registration factory's identity generation.</summary>
    public static User FromPersistence(Guid id, Email email, string passwordHash, DateTime createdAtUtc) => new()
    {
        Id = id,
        Email = email,
        PasswordHash = passwordHash,
        CreatedAtUtc = createdAtUtc,
    };
}
