using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Exceptions;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Identity;

public sealed record RegisterUserCommand(string Email, string Password);

public sealed class RegisterUserHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
{
    public async Task<Guid> HandleAsync(RegisterUserCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length < 8)
        {
            throw new ArgumentException("Password must be at least 8 characters long.", nameof(command));
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
