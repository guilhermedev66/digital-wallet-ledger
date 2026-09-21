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

    // Same caps as RegisterUserHandler, checked before Email.Parse/IPasswordHasher.Verify run.
    // Mapped to the same InvalidCredentialsException as bad credentials (not a distinct 400) -
    // login never gives an unauthenticated caller a different response shape for "malformed
    // input" versus "wrong password", same reasoning as the Email.Parse catch below.
    private const int MaxEmailLength = 320;
    private const int MaxPasswordLength = 128;

    public async Task<AuthResultDto> HandleAsync(LoginCommand command, CancellationToken ct)
    {
        // Only guards against null (would NRE below) and oversized input (the actual
        // CPU-amplification concern) - deliberately NOT rejecting empty/short input here,
        // so an empty password still flows through the same dummy-hash-verify path as any
        // other wrong password instead of getting a distinguishable fast-reject timing.
        if (command.Email is null || command.Email.Length > MaxEmailLength
            || command.Password is null || command.Password.Length > MaxPasswordLength)
        {
            throw new InvalidCredentialsException();
        }

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
