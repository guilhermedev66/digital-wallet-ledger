using Moq;
using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Ledger;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Tests.Ledger;

public class SimulateFundingHandlerTests
{
    private const long MaxAmountMinorUnits = 1_000_000_00;
    private const int MaxIdempotencyKeyLength = 128;

    private readonly Mock<IWalletRepository> _walletRepository = new();
    private readonly Mock<ITransactionRepository> _transactionRepository = new();

    private SimulateFundingHandler CreateHandler() => new(_walletRepository.Object, _transactionRepository.Object);

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(MaxAmountMinorUnits + 1)]
    public async Task HandleAsync_AmountOutOfRange_ThrowsArgumentException_WithoutCallingRepository(long amount)
    {
        var handler = CreateHandler();
        var command = new SimulateFundingCommand(Guid.NewGuid(), Guid.NewGuid(), amount, "idem-key");

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        _walletRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_IdempotencyKeyMissing_ThrowsArgumentException_WithoutCallingRepository(string? idempotencyKey)
    {
        var handler = CreateHandler();
        var command = new SimulateFundingCommand(Guid.NewGuid(), Guid.NewGuid(), 1_000, idempotencyKey!);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        _walletRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_IdempotencyKeyOverMaxLength_ThrowsArgumentException_WithoutCallingRepository()
    {
        var handler = CreateHandler();
        var oversizedKey = new string('a', MaxIdempotencyKeyLength + 1);
        var command = new SimulateFundingCommand(Guid.NewGuid(), Guid.NewGuid(), 1_000, oversizedKey);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        _walletRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WalletDoesNotExist_ReturnsNull()
    {
        _walletRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LedgerAccount?)null);

        var handler = CreateHandler();
        var command = new SimulateFundingCommand(Guid.NewGuid(), Guid.NewGuid(), 1_000, "idem-key");

        var result = await handler.HandleAsync(command, CancellationToken.None);

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
        var command = new SimulateFundingCommand(requestingUserId, wallet.Id, 1_000, "idem-key");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_NoSystemFundingAccountForCurrency_ThrowsInvalidOperationException()
    {
        var ownerId = Guid.NewGuid();
        var wallet = LedgerAccount.Open(ownerId, Currency.Usd, "My wallet");

        _walletRepository
            .Setup(r => r.GetByIdAsync(wallet.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);
        _walletRepository
            .Setup(r => r.GetSystemFundingAccountAsync(Currency.Usd, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LedgerAccount?)null);

        var handler = CreateHandler();
        var command = new SimulateFundingCommand(ownerId, wallet.Id, 1_000, "idem-key");

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WalletOwnedByCallerAndFundingAccountExists_PostsBalancedTransactionAndReturnsDto()
    {
        var ownerId = Guid.NewGuid();
        var wallet = LedgerAccount.Open(ownerId, Currency.Usd, "My wallet");
        var fundingAccount = LedgerAccount.OpenSystemFundingAccount(Currency.Usd, "USD Funding Source");

        _walletRepository
            .Setup(r => r.GetByIdAsync(wallet.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);
        _walletRepository
            .Setup(r => r.GetSystemFundingAccountAsync(Currency.Usd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fundingAccount);

        Transaction? addedTransaction = null;
        _transactionRepository
            .Setup(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .Callback<Transaction, CancellationToken>((t, _) => addedTransaction = t)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var command = new SimulateFundingCommand(ownerId, wallet.Id, 5_000, "idem-key");

        var dto = await handler.HandleAsync(command, CancellationToken.None);

        _transactionRepository.Verify(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(addedTransaction);
        Assert.Equal(TransactionType.SimulatedFunding, addedTransaction!.Type);
        Assert.Equal(2, addedTransaction.Entries.Count);

        Assert.Contains(addedTransaction.Entries, e =>
            e.AccountId == wallet.Id && e.Direction == LedgerEntryDirection.Debit &&
            e.AmountMinorUnits == 5_000 && e.Currency == Currency.Usd);
        Assert.Contains(addedTransaction.Entries, e =>
            e.AccountId == fundingAccount.Id && e.Direction == LedgerEntryDirection.Credit &&
            e.AmountMinorUnits == 5_000 && e.Currency == Currency.Usd);

        Assert.NotNull(dto);
        Assert.Equal(addedTransaction.Id, dto!.Id);
        Assert.Equal(nameof(TransactionType.SimulatedFunding), dto.Type);
        Assert.Equal(2, dto.Entries.Count);
    }
}
