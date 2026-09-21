using Moq;
using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Exceptions;
using WalletLedger.Application.Identity;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Tests.Identity;

public class RegisterUserHandlerTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();

    private RegisterUserHandler CreateHandler() => new(_userRepository.Object, _passwordHasher.Object);

    [Theory]
    [InlineData("")]
    [InlineData("short1")]
    [InlineData("1234567")]
    public async Task HandleAsync_PasswordUnderEightChars_ThrowsArgumentException(string password)
    {
        var handler = CreateHandler();
        var command = new RegisterUserCommand("user@example.com", password);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        _userRepository.Verify(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PasswordOverMaxLength_ThrowsArgumentExceptionWithoutHashing()
    {
        var handler = CreateHandler();
        var oversizedPassword = new string('a', 129);
        var command = new RegisterUserCommand("user@example.com", oversizedPassword);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        _passwordHasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EmailOverMaxLength_ThrowsArgumentExceptionWithoutParsing()
    {
        var handler = CreateHandler();
        var oversizedLocalPart = new string('a', 315);
        var command = new RegisterUserCommand($"{oversizedLocalPart}@example.com", "password123");

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        _userRepository.Verify(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EmailAlreadyRegistered_ThrowsEmailAlreadyRegisteredException()
    {
        var existingUser = User.Register(Email.Parse("user@example.com"), "some-hash");
        _userRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var handler = CreateHandler();
        var command = new RegisterUserCommand("user@example.com", "password123");

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(() => handler.HandleAsync(command, CancellationToken.None));

        _userRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NewEmail_HashesPasswordAndPersistsUserAndReturnsId()
    {
        _userRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _passwordHasher
            .Setup(h => h.Hash("password123"))
            .Returns("hashed-password123");

        User? addedUser = null;
        _userRepository
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => addedUser = u)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var command = new RegisterUserCommand("USER@Example.com", "password123");

        var resultId = await handler.HandleAsync(command, CancellationToken.None);

        _passwordHasher.Verify(h => h.Hash("password123"), Times.Once);
        _userRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);

        Assert.NotNull(addedUser);
        Assert.Equal("user@example.com", addedUser!.Email.Value);
        Assert.Equal("hashed-password123", addedUser.PasswordHash);
        Assert.Equal(addedUser.Id, resultId);
    }
}
