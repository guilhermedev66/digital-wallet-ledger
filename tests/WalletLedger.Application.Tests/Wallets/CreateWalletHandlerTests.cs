using Moq;
using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Wallets;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Tests.Wallets;

public class CreateWalletHandlerTests
{
    [Fact]
    public async Task HandleAsync_PersistsAccountOwnedByCaller_AndReturnsMatchingDto()
    {
        var walletRepository = new Mock<IWalletRepository>();
        LedgerAccount? addedAccount = null;
        walletRepository
            .Setup(r => r.AddAsync(It.IsAny<LedgerAccount>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerAccount, CancellationToken>((a, _) => addedAccount = a)
            .Returns(Task.CompletedTask);

        var handler = new CreateWalletHandler(walletRepository.Object);
        var ownerId = Guid.NewGuid();
        var command = new CreateWalletCommand(ownerId, Currency.Brl, "Travel fund");

        var dto = await handler.HandleAsync(command, CancellationToken.None);

        walletRepository.Verify(r => r.AddAsync(It.IsAny<LedgerAccount>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(addedAccount);
        Assert.Equal(ownerId, addedAccount!.OwnerUserId);
        Assert.Equal(Currency.Brl, addedAccount.Currency);
        Assert.Equal("Travel fund", addedAccount.DisplayName);

        Assert.Equal(addedAccount.Id, dto.Id);
        Assert.Equal(ownerId, dto.OwnerUserId);
        Assert.Equal(nameof(Currency.Brl), dto.Currency);
        Assert.Equal("Travel fund", dto.DisplayName);
    }
}
