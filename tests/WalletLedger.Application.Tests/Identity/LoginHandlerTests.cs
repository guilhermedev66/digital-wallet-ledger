using Moq;
using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Exceptions;
using WalletLedger.Application.Identity;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Tests.Identity;

public class LoginHandlerTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGenerator = new();

    private LoginHandler CreateHandler() => new(_userRepository.Object, _passwordHasher.Object, _jwtTokenGenerator.Object);

    [Fact]
    public async Task HandleAsync_UnknownEmail_ThrowsInvalidCredentialsAndStillVerifiesAgainstDummyHash()
    {
        _userRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _passwordHasher
            .Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        var handler = CreateHandler();
        var command = new LoginCommand("unknown@example.com", "whatever123");

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => handler.HandleAsync(command, CancellationToken.None));

        _passwordHasher.Verify(h => h.Verify("whatever123", It.IsAny<string>()), Times.Once);
        _jwtTokenGenerator.Verify(j => j.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PasswordOverMaxLength_ThrowsInvalidCredentialsWithoutVerifying()
    {
        var handler = CreateHandler();
        var oversizedPassword = new string('a', 129);
        var command = new LoginCommand("user@example.com", oversizedPassword);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => handler.HandleAsync(command, CancellationToken.None));

        _passwordHasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _userRepository.Verify(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EmailOverMaxLength_ThrowsInvalidCredentialsWithoutLookup()
    {
        var handler = CreateHandler();
        var oversizedLocalPart = new string('a', 315);
        var command = new LoginCommand($"{oversizedLocalPart}@example.com", "whatever123");

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => handler.HandleAsync(command, CancellationToken.None));

        _userRepository.Verify(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WrongPassword_ThrowsInvalidCredentialsException()
    {
        var user = User.Register(Email.Parse("user@example.com"), "real-hash");
        _userRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher
            .Setup(h => h.Verify("wrong-password", "real-hash"))
            .Returns(false);

        var handler = CreateHandler();
        var command = new LoginCommand("user@example.com", "wrong-password");

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => handler.HandleAsync(command, CancellationToken.None));

        _jwtTokenGenerator.Verify(j => j.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ValidCredentials_ReturnsAuthResultFromGeneratedToken()
    {
        var user = User.Register(Email.Parse("user@example.com"), "real-hash");
        _userRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher
            .Setup(h => h.Verify("correct-password", "real-hash"))
            .Returns(true);

        var expiresAt = DateTime.UtcNow.AddHours(1);
        _jwtTokenGenerator
            .Setup(j => j.GenerateToken(user))
            .Returns(new AuthToken("signed-jwt", expiresAt));

        var handler = CreateHandler();
        var command = new LoginCommand("user@example.com", "correct-password");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("user@example.com", result.Email);
        Assert.Equal("signed-jwt", result.AccessToken);
        Assert.Equal(expiresAt, result.ExpiresAtUtc);
    }
}
