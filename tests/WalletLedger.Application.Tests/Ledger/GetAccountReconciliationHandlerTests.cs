using Moq;
using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Ledger;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Tests.Ledger;

public class GetAccountReconciliationHandlerTests
{
    private readonly Mock<IWalletRepository> _walletRepository = new();
    private readonly Mock<ITransactionRepository> _transactionRepository = new();

    private GetAccountReconciliationHandler CreateHandler() => new(_walletRepository.Object, _transactionRepository.Object);

    [Fact]
    public async Task HandleAsync_WalletDoesNotExist_ReturnsNull()
    {
        _walletRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((LedgerAccount?)null);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetAccountReconciliationQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_WalletBelongsToDifferentOwner_NonAdmin_ReturnsNull()
    {
        var wallet = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd, "Someone else's");
        _walletRepository.Setup(r => r.GetByIdAsync(wallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(wallet);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetAccountReconciliationQuery(Guid.NewGuid(), wallet.Id, CallerIsAdmin: false), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_WalletBelongsToDifferentOwner_Admin_ReturnsReport()
    {
        var wallet = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd, "Someone else's");
        _walletRepository.Setup(r => r.GetByIdAsync(wallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(wallet);
        _transactionRepository
            .Setup(r => r.ListEntriesForTransactionsTouchingAccountAsync(wallet.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<LedgerEntryProjection>)[]);
        _transactionRepository.Setup(r => r.GetAccountBalanceAsync(wallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetAccountReconciliationQuery(Guid.NewGuid(), wallet.Id, CallerIsAdmin: true), CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task HandleAsync_ProjectedMatchesRecomputed_IsClean()
    {
        var ownerId = Guid.NewGuid();
        var wallet = LedgerAccount.Open(ownerId, Currency.Usd, "Mine");
        var other = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        var entries = new List<LedgerEntryProjection>
        {
            new(transactionId, wallet.Id, LedgerEntryDirection.Debit, 5_000, Currency.Usd),
            new(transactionId, other, LedgerEntryDirection.Credit, 5_000, Currency.Usd),
        };

        _walletRepository.Setup(r => r.GetByIdAsync(wallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(wallet);
        _transactionRepository
            .Setup(r => r.ListEntriesForTransactionsTouchingAccountAsync(wallet.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        _transactionRepository.Setup(r => r.GetAccountBalanceAsync(wallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(5_000);

        var handler = CreateHandler();
        var report = await handler.HandleAsync(new GetAccountReconciliationQuery(ownerId, wallet.Id), CancellationToken.None);

        Assert.NotNull(report);
        Assert.True(report!.IsClean);
        Assert.Empty(report.UnbalancedTransactions);
        var accountReport = Assert.Single(report.Accounts);
        Assert.Equal(5_000, accountReport.ProjectedBalanceMinorUnits);
        Assert.Equal(5_000, accountReport.RecomputedBalanceMinorUnits);
        Assert.Equal(0, accountReport.DriftMinorUnits);
        Assert.True(accountReport.IsBalanced);
    }

    [Fact]
    public async Task HandleAsync_ProjectedDiffersFromRecomputed_FlagsDrift()
    {
        var ownerId = Guid.NewGuid();
        var wallet = LedgerAccount.Open(ownerId, Currency.Usd, "Mine");
        var other = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        var entries = new List<LedgerEntryProjection>
        {
            new(transactionId, wallet.Id, LedgerEntryDirection.Debit, 5_000, Currency.Usd),
            new(transactionId, other, LedgerEntryDirection.Credit, 5_000, Currency.Usd),
        };

        _walletRepository.Setup(r => r.GetByIdAsync(wallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(wallet);
        _transactionRepository
            .Setup(r => r.ListEntriesForTransactionsTouchingAccountAsync(wallet.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        // Simulates a hypothetical future cached projection diverging from the raw ledger.
        _transactionRepository.Setup(r => r.GetAccountBalanceAsync(wallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(4_000);

        var handler = CreateHandler();
        var report = await handler.HandleAsync(new GetAccountReconciliationQuery(ownerId, wallet.Id), CancellationToken.None);

        Assert.NotNull(report);
        Assert.False(report!.IsClean);
        var accountReport = Assert.Single(report.Accounts);
        Assert.False(accountReport.IsBalanced);
        Assert.Equal(-1_000, accountReport.DriftMinorUnits);
    }

    [Fact]
    public async Task HandleAsync_UnbalancedTransactionAmongTouchingEntries_IsFlagged()
    {
        // Simulates raw DB tampering / a bug bypassing Transaction.Post - not constructible via
        // the domain layer, which is exactly why ReconciliationEngine works off raw projections.
        var ownerId = Guid.NewGuid();
        var wallet = LedgerAccount.Open(ownerId, Currency.Usd, "Mine");
        var other = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        var entries = new List<LedgerEntryProjection>
        {
            new(transactionId, wallet.Id, LedgerEntryDirection.Debit, 5_000, Currency.Usd),
            new(transactionId, other, LedgerEntryDirection.Credit, 4_000, Currency.Usd), // tampered: should be 5,000
        };

        _walletRepository.Setup(r => r.GetByIdAsync(wallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(wallet);
        _transactionRepository
            .Setup(r => r.ListEntriesForTransactionsTouchingAccountAsync(wallet.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        _transactionRepository.Setup(r => r.GetAccountBalanceAsync(wallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(5_000);

        var handler = CreateHandler();
        var report = await handler.HandleAsync(new GetAccountReconciliationQuery(ownerId, wallet.Id), CancellationToken.None);

        Assert.NotNull(report);
        Assert.False(report!.IsClean);
        var unbalanced = Assert.Single(report.UnbalancedTransactions);
        Assert.Equal(transactionId, unbalanced.TransactionId);
        Assert.Equal(5_000, unbalanced.TotalDebitMinorUnits);
        Assert.Equal(4_000, unbalanced.TotalCreditMinorUnits);
    }
}
