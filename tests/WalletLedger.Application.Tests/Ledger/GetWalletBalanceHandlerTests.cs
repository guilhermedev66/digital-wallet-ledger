using Moq;
using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Ledger;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Tests.Ledger;

public class GetWalletBalanceHandlerTests
{
    private readonly Mock<IWalletRepository> _walletRepository = new();
    private readonly Mock<ITransactionRepository> _transactionRepository = new();

    private GetWalletBalanceHandler CreateHandler() => new(_walletRepository.Object, _transactionRepository.Object);

    [Fact]
    public async Task HandleAsync_WalletDoesNotExist_ReturnsNull()
    {
        _walletRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LedgerAccount?)null);

        var handler = CreateHandler();

        var result = await handler.HandleAsync(new GetWalletBalanceQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_WalletBelongsToDifferentOwner_ReturnsNullNotForbidden()
    {
        var actualOwnerId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var wallet = LedgerAccount.Open(actualOwnerId, Currency.Usd, "Someone else's wallet");

        _walletRepository
            .Setup(r => r.GetByIdAsync(wallet.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);

        var handler = CreateHandler();

        var result = await handler.HandleAsync(new GetWalletBalanceQuery(requestingUserId, wallet.Id), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_WalletOwnedByCaller_ReturnsBalanceFromTransactionRepository()
    {
        var ownerId = Guid.NewGuid();
        var wallet = LedgerAccount.Open(ownerId, Currency.Brl, "My wallet");

        _walletRepository
            .Setup(r => r.GetByIdAsync(wallet.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);
        _transactionRepository
            .Setup(r => r.GetAccountBalanceAsync(wallet.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(12_345L);

        var handler = CreateHandler();

        var result = await handler.HandleAsync(new GetWalletBalanceQuery(ownerId, wallet.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(wallet.Id, result!.WalletId);
        Assert.Equal(nameof(Currency.Brl), result.Currency);
        Assert.Equal(12_345L, result.BalanceMinorUnits);
    }

    [Fact]
    public async Task HandleAsync_NegativeBalance_IsReturnedAsIs_NotTreatedAsAnError()
    {
        // A SystemFunding account's balance is expected to go negative (it only ever gives
        // money away) - the handler must not clamp, reject, or special-case a negative value.
        var ownerId = Guid.NewGuid();
        var wallet = LedgerAccount.Open(ownerId, Currency.Usd, "My wallet");

        _walletRepository
            .Setup(r => r.GetByIdAsync(wallet.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);
        _transactionRepository
            .Setup(r => r.GetAccountBalanceAsync(wallet.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(-500L);

        var handler = CreateHandler();

        var result = await handler.HandleAsync(new GetWalletBalanceQuery(ownerId, wallet.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(-500L, result!.BalanceMinorUnits);
    }
}
