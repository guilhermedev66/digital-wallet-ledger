using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Dtos;
using WalletLedger.Application.Exceptions;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Identity;

public sealed record LoginCommand(string Email, string Password);

public sealed class LoginHandler(IUserRepository userRepository, IPasswordHasher passwordHasher, IJwtTokenGenerator jwtTokenGenerator)
{
    // Fixed dummy hash verified when the email is unknown, so a login attempt against a
    // non-existent account takes the same code path (and roughly the same time) as a wrong
    // password against a real one — the response never reveals whether the email is registered.
    private const string DummyHashForUnknownUser =
        "AQAAAAIAAYagAAAAEP5b3fq6t0k1m9m8s0f7v9m3d3e4t8u2w1x9y7z6a5b4c3d2e1f0";

    public async Task<AuthResultDto> HandleAsync(LoginCommand command, CancellationToken ct)
    {
        Email email;
        try
        {
            email = Email.Parse(command.Email);
        }
        catch (ArgumentException)
        {
            throw new InvalidCredentialsException();
        }

        var user = await userRepository.GetByEmailAsync(email, ct);

        var passwordToVerify = command.Password ?? string.Empty;
        var hashToVerifyAgainst = user?.PasswordHash ?? DummyHashForUnknownUser;
        var passwordMatches = passwordHasher.Verify(passwordToVerify, hashToVerifyAgainst);

        if (user is null || !passwordMatches)
        {
            throw new InvalidCredentialsException();
        }

        var token = jwtTokenGenerator.GenerateToken(user);

        return new AuthResultDto(user.Id, user.Email.Value, token.AccessToken, token.ExpiresAtUtc);
    }
}
