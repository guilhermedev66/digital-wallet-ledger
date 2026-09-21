using Moq;
using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Wallets;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Tests.Wallets;

public class GetWalletByIdHandlerTests
{
    [Fact]
    public async Task HandleAsync_WalletDoesNotExist_ReturnsNull()
    {
        var walletRepository = new Mock<IWalletRepository>();
        walletRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LedgerAccount?)null);

        var handler = new GetWalletByIdHandler(walletRepository.Object);

        var result = await handler.HandleAsync(new GetWalletByIdQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_WalletBelongsToDifferentOwner_ReturnsNullNotForbidden()
    {
        var actualOwnerId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var account = LedgerAccount.Open(actualOwnerId, Currency.Usd, "Someone else's wallet");

        var walletRepository = new Mock<IWalletRepository>();
        walletRepository
            .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var handler = new GetWalletByIdHandler(walletRepository.Object);

        var result = await handler.HandleAsync(new GetWalletByIdQuery(requestingUserId, account.Id), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_CallerIsAdmin_ReturnsDtoForSomeoneElsesWallet()
    {
        var actualOwnerId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var account = LedgerAccount.Open(actualOwnerId, Currency.Usd, "Someone else's wallet");

        var walletRepository = new Mock<IWalletRepository>();
        walletRepository
            .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var handler = new GetWalletByIdHandler(walletRepository.Object);

        var result = await handler.HandleAsync(
            new GetWalletByIdQuery(adminUserId, account.Id, CallerIsAdmin: true), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(account.Id, result!.Id);
        Assert.Equal(actualOwnerId, result.OwnerUserId);
    }

    [Fact]
    public async Task HandleAsync_WalletBelongsToRequestingOwner_ReturnsDto()
    {
        var ownerId = Guid.NewGuid();
        var account = LedgerAccount.Open(ownerId, Currency.Usd, "My wallet");

        var walletRepository = new Mock<IWalletRepository>();
        walletRepository
            .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var handler = new GetWalletByIdHandler(walletRepository.Object);

        var result = await handler.HandleAsync(new GetWalletByIdQuery(ownerId, account.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(account.Id, result!.Id);
        Assert.Equal(ownerId, result.OwnerUserId);
        Assert.Equal("My wallet", result.DisplayName);
    }
}
