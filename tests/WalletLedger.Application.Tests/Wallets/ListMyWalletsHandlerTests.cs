using Moq;
using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Wallets;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Tests.Wallets;

public class ListMyWalletsHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsDtosForEveryAccountFromRepository_PreservingOrder()
    {
        var ownerId = Guid.NewGuid();
        var account1 = LedgerAccount.Open(ownerId, Currency.Usd, "Main");
        var account2 = LedgerAccount.Open(ownerId, Currency.Brl, "Savings");

        var walletRepository = new Mock<IWalletRepository>();
        walletRepository
            .Setup(r => r.ListByOwnerAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LedgerAccount> { account1, account2 });

        var handler = new ListMyWalletsHandler(walletRepository.Object);

        var result = await handler.HandleAsync(new ListMyWalletsQuery(ownerId), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(account1.Id, result[0].Id);
        Assert.Equal("Main", result[0].DisplayName);
        Assert.Equal(account2.Id, result[1].Id);
        Assert.Equal("Savings", result[1].DisplayName);
    }

    [Fact]
    public async Task HandleAsync_NoAccounts_ReturnsEmptyList()
    {
        var ownerId = Guid.NewGuid();
        var walletRepository = new Mock<IWalletRepository>();
        walletRepository
            .Setup(r => r.ListByOwnerAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LedgerAccount>());

        var handler = new ListMyWalletsHandler(walletRepository.Object);

        var result = await handler.HandleAsync(new ListMyWalletsQuery(ownerId), CancellationToken.None);

        Assert.Empty(result);
    }
}
