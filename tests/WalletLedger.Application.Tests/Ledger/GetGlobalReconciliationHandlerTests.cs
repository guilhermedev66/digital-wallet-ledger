using Moq;
using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Ledger;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Tests.Ledger;

public class GetGlobalReconciliationHandlerTests
{
    private readonly Mock<IWalletRepository> _walletRepository = new();
    private readonly Mock<ITransactionRepository> _transactionRepository = new();

    private GetGlobalReconciliationHandler CreateHandler() => new(_walletRepository.Object, _transactionRepository.Object);

    [Fact]
    public async Task HandleAsync_NonAdmin_ReturnsNull_WithoutQueryingRepositories()
    {
        var handler = CreateHandler();

        var result = await handler.HandleAsync(new GetGlobalReconciliationQuery(CallerIsAdmin: false), CancellationToken.None);

        Assert.Null(result);
        _walletRepository.Verify(r => r.ListAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        _transactionRepository.Verify(r => r.ListAllEntriesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Admin_ReconcilesEveryAccount()
    {
        var walletA = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd, "A");
        var walletB = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd, "B");
        var transactionId = Guid.NewGuid();

        var entries = new List<LedgerEntryProjection>
        {
            new(transactionId, walletA.Id, LedgerEntryDirection.Credit, 1_000, Currency.Usd),
            new(transactionId, walletB.Id, LedgerEntryDirection.Debit, 1_000, Currency.Usd),
        };

        _walletRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([walletA, walletB]);
        _transactionRepository.Setup(r => r.ListAllEntriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(entries);
        _transactionRepository.Setup(r => r.GetAccountBalanceAsync(walletA.Id, It.IsAny<CancellationToken>())).ReturnsAsync(-1_000);
        _transactionRepository.Setup(r => r.GetAccountBalanceAsync(walletB.Id, It.IsAny<CancellationToken>())).ReturnsAsync(1_000);

        var handler = CreateHandler();
        var report = await handler.HandleAsync(new GetGlobalReconciliationQuery(CallerIsAdmin: true), CancellationToken.None);

        Assert.NotNull(report);
        Assert.True(report!.IsClean);
        Assert.Equal(2, report.Accounts.Count);
        Assert.All(report.Accounts, a => Assert.True(a.IsBalanced));
    }

    [Fact]
    public async Task HandleAsync_Admin_UnbalancedTransactionAnywhere_MarksReportNotClean()
    {
        var walletA = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd, "A");
        var transactionId = Guid.NewGuid();

        var entries = new List<LedgerEntryProjection>
        {
            new(transactionId, walletA.Id, LedgerEntryDirection.Debit, 1_000, Currency.Usd),
            new(transactionId, Guid.NewGuid(), LedgerEntryDirection.Credit, 900, Currency.Usd),
        };

        _walletRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([walletA]);
        _transactionRepository.Setup(r => r.ListAllEntriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(entries);
        _transactionRepository.Setup(r => r.GetAccountBalanceAsync(walletA.Id, It.IsAny<CancellationToken>())).ReturnsAsync(1_000);

        var handler = CreateHandler();
        var report = await handler.HandleAsync(new GetGlobalReconciliationQuery(CallerIsAdmin: true), CancellationToken.None);

        Assert.NotNull(report);
        Assert.False(report!.IsClean);
        Assert.Single(report.UnbalancedTransactions);
    }
}
