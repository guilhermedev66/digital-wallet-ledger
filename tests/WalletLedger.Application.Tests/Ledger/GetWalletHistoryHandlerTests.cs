using Moq;
using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Ledger;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Tests.Ledger;

public class GetWalletHistoryHandlerTests
{
    private readonly Mock<IWalletRepository> _walletRepository = new();
    private readonly Mock<ITransactionRepository> _transactionRepository = new();

    private GetWalletHistoryHandler CreateHandler() => new(_walletRepository.Object, _transactionRepository.Object);

    [Fact]
    public async Task HandleAsync_PageBelowOne_ThrowsArgumentException()
    {
        var handler = CreateHandler();
        var query = new GetWalletHistoryQuery(Guid.NewGuid(), Guid.NewGuid(), Page: 0, PageSize: 20);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(query, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_FromUtcAfterToUtc_ThrowsArgumentException_WithoutCallingRepository()
    {
        var handler = CreateHandler();
        var query = new GetWalletHistoryQuery(
            Guid.NewGuid(), Guid.NewGuid(), Page: 1, PageSize: 20,
            FromUtc: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(query, CancellationToken.None));

        _walletRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WalletDoesNotExist_ReturnsNull()
    {
        _walletRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((LedgerAccount?)null);

        var handler = CreateHandler();
        var query = new GetWalletHistoryQuery(Guid.NewGuid(), Guid.NewGuid(), Page: 1, PageSize: 20);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_PassesFiltersThroughToRepository()
    {
        var ownerId = Guid.NewGuid();
        var wallet = LedgerAccount.Open(ownerId, Currency.Usd, "Mine");
        var fromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        _walletRepository.Setup(r => r.GetByIdAsync(wallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(wallet);
        _transactionRepository
            .Setup(r => r.ListTransactionsForAccountAsync(wallet.Id, 2, 10, fromUtc, toUtc, TransactionType.Transfer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Transaction>)[], 0));

        var handler = CreateHandler();
        var query = new GetWalletHistoryQuery(ownerId, wallet.Id, Page: 2, PageSize: 10, fromUtc, toUtc, TransactionType.Transfer);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        Assert.NotNull(result);
        _transactionRepository.Verify(
            r => r.ListTransactionsForAccountAsync(wallet.Id, 2, 10, fromUtc, toUtc, TransactionType.Transfer, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NoFilters_PassesNullsThrough()
    {
        var ownerId = Guid.NewGuid();
        var wallet = LedgerAccount.Open(ownerId, Currency.Usd, "Mine");

        _walletRepository.Setup(r => r.GetByIdAsync(wallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(wallet);
        _transactionRepository
            .Setup(r => r.ListTransactionsForAccountAsync(wallet.Id, 1, 20, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Transaction>)[], 0));

        var handler = CreateHandler();
        var query = new GetWalletHistoryQuery(ownerId, wallet.Id, Page: 1, PageSize: 20);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        Assert.NotNull(result);
        _transactionRepository.Verify(
            r => r.ListTransactionsForAccountAsync(wallet.Id, 1, 20, null, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
