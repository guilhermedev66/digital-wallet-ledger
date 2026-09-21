using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Exceptions;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Identity;

public sealed record RegisterUserCommand(string Email, string Password);

public sealed class RegisterUserHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
{
    // Checked before Email.Parse/IPasswordHasher.Hash run at all - an unauthenticated caller
    // sending an oversized payload shouldn't get to force disproportionate parsing/hashing work.
    private const int MaxEmailLength = 320; // matches the Users.Email column and RFC 5321
    private const int MaxPasswordLength = 128;

    public async Task<Guid> HandleAsync(RegisterUserCommand command, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(command.Email) || command.Email.Length > MaxEmailLength)
        {
            throw new ArgumentException($"Email must be 1-{MaxEmailLength} characters long.", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length is < 8 or > MaxPasswordLength)
        {
            throw new ArgumentException($"Password must be 8-{MaxPasswordLength} characters long.", nameof(command));
        }

        var email = Email.Parse(command.Email);

        var existing = await userRepository.GetByEmailAsync(email, ct);
        if (existing is not null)
        {
            throw new EmailAlreadyRegisteredException();
        }

        var passwordHash = passwordHasher.Hash(command.Password);
        var user = User.Register(email, passwordHash);

        await userRepository.AddAsync(user, ct);

        return user.Id;
    }
}
