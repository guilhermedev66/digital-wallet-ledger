using Moq;
using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Exceptions;
using WalletLedger.Application.Ledger;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Tests.Ledger;

public class TransferHandlerTests
{
    private const long MaxAmountMinorUnits = 1_000_000_00;
    private const int MaxIdempotencyKeyLength = 128;

    private readonly Mock<IWalletRepository> _walletRepository = new();
    private readonly Mock<ITransactionRepository> _transactionRepository = new();

    private TransferHandler CreateHandler() => new(_walletRepository.Object, _transactionRepository.Object);

    private static Transaction BuildTransferTransaction(
        Guid ownerId, Guid sourceId, Guid destinationId, long amount, Currency currency, string idempotencyKey = "idem-key") =>
        Transaction.Post(
            ownerId, idempotencyKey, TransactionType.Transfer,
            [
                new LedgerEntryLine(sourceId, LedgerEntryDirection.Debit, amount, currency),
                new LedgerEntryLine(destinationId, LedgerEntryDirection.Credit, amount, currency),
            ]);

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(MaxAmountMinorUnits + 1)]
    public async Task HandleAsync_AmountOutOfRange_ThrowsArgumentException_WithoutCallingRepository(long amount)
    {
        var handler = CreateHandler();
        var command = new TransferCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), amount, "idem-key");

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
        var command = new TransferCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1_000, idempotencyKey!);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        _walletRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_IdempotencyKeyOverMaxLength_ThrowsArgumentException_WithoutCallingRepository()
    {
        var handler = CreateHandler();
        var oversizedKey = new string('a', MaxIdempotencyKeyLength + 1);
        var command = new TransferCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1_000, oversizedKey);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        _walletRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SourceEqualsDestination_ThrowsArgumentException_WithoutCallingRepository()
    {
        var handler = CreateHandler();
        var walletId = Guid.NewGuid();
        var command = new TransferCommand(Guid.NewGuid(), walletId, walletId, 1_000, "idem-key");

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        _walletRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SourceWalletDoesNotExist_ReturnsNull()
    {
        _walletRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((LedgerAccount?)null);

        var handler = CreateHandler();
        var command = new TransferCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1_000, "idem-key");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_SourceWalletBelongsToDifferentOwner_ReturnsNull()
    {
        var actualOwnerId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var sourceWallet = LedgerAccount.Open(actualOwnerId, Currency.Usd, "Someone else's wallet");

        _walletRepository.Setup(r => r.GetByIdAsync(sourceWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sourceWallet);

        var handler = CreateHandler();
        var command = new TransferCommand(requestingUserId, sourceWallet.Id, Guid.NewGuid(), 1_000, "idem-key");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_DestinationWalletDoesNotExist_ThrowsArgumentException()
    {
        var ownerId = Guid.NewGuid();
        var sourceWallet = LedgerAccount.Open(ownerId, Currency.Usd, "My wallet");
        var destinationId = Guid.NewGuid();

        _walletRepository.Setup(r => r.GetByIdAsync(sourceWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sourceWallet);
        _walletRepository.Setup(r => r.GetByIdAsync(destinationId, It.IsAny<CancellationToken>())).ReturnsAsync((LedgerAccount?)null);

        var handler = CreateHandler();
        var command = new TransferCommand(ownerId, sourceWallet.Id, destinationId, 1_000, "idem-key");

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_DestinationIsASystemFundingAccount_ThrowsArgumentException()
    {
        var ownerId = Guid.NewGuid();
        var sourceWallet = LedgerAccount.Open(ownerId, Currency.Usd, "My wallet");
        var systemAccount = LedgerAccount.OpenSystemFundingAccount(Currency.Usd, "USD Funding Source");

        _walletRepository.Setup(r => r.GetByIdAsync(sourceWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sourceWallet);
        _walletRepository.Setup(r => r.GetByIdAsync(systemAccount.Id, It.IsAny<CancellationToken>())).ReturnsAsync(systemAccount);

        var handler = CreateHandler();
        var command = new TransferCommand(ownerId, sourceWallet.Id, systemAccount.Id, 1_000, "idem-key");

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_CurrencyMismatch_ThrowsArgumentException()
    {
        var ownerId = Guid.NewGuid();
        var sourceWallet = LedgerAccount.Open(ownerId, Currency.Usd, "USD wallet");
        var destinationWallet = LedgerAccount.Open(Guid.NewGuid(), Currency.Brl, "BRL wallet");

        _walletRepository.Setup(r => r.GetByIdAsync(sourceWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sourceWallet);
        _walletRepository.Setup(r => r.GetByIdAsync(destinationWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(destinationWallet);

        var handler = CreateHandler();
        var command = new TransferCommand(ownerId, sourceWallet.Id, destinationWallet.Id, 1_000, "idem-key");

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_IdempotencyKeyAlreadyUsedWithMatchingParameters_ReturnsExistingDto_WithoutPosting()
    {
        var ownerId = Guid.NewGuid();
        var sourceWallet = LedgerAccount.Open(ownerId, Currency.Usd, "Source");
        var destinationWallet = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd, "Destination");
        var existing = BuildTransferTransaction(ownerId, sourceWallet.Id, destinationWallet.Id, 5_000, Currency.Usd);

        _walletRepository.Setup(r => r.GetByIdAsync(sourceWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sourceWallet);
        _walletRepository.Setup(r => r.GetByIdAsync(destinationWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(destinationWallet);
        _transactionRepository
            .Setup(r => r.FindByIdempotencyKeyAsync(ownerId, "idem-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = CreateHandler();
        var command = new TransferCommand(ownerId, sourceWallet.Id, destinationWallet.Id, 5_000, "idem-key");

        var dto = await handler.HandleAsync(command, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(existing.Id, dto!.Id);
        _transactionRepository.Verify(
            r => r.PostTransferIfSufficientFundsAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<Transaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_IdempotencyKeyAlreadyUsedWithDifferentParameters_ThrowsConflict_WithoutPosting()
    {
        var ownerId = Guid.NewGuid();
        var sourceWallet = LedgerAccount.Open(ownerId, Currency.Usd, "Source");
        var destinationWallet = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd, "Destination");
        var existing = BuildTransferTransaction(ownerId, sourceWallet.Id, destinationWallet.Id, 2_000, Currency.Usd); // different amount

        _walletRepository.Setup(r => r.GetByIdAsync(sourceWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sourceWallet);
        _walletRepository.Setup(r => r.GetByIdAsync(destinationWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(destinationWallet);
        _transactionRepository
            .Setup(r => r.FindByIdempotencyKeyAsync(ownerId, "idem-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = CreateHandler();
        var command = new TransferCommand(ownerId, sourceWallet.Id, destinationWallet.Id, 5_000, "idem-key");

        await Assert.ThrowsAsync<IdempotencyKeyConflictException>(() => handler.HandleAsync(command, CancellationToken.None));

        _transactionRepository.Verify(
            r => r.PostTransferIfSufficientFundsAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<Transaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_InsufficientFunds_ThrowsInsufficientFundsException()
    {
        var ownerId = Guid.NewGuid();
        var sourceWallet = LedgerAccount.Open(ownerId, Currency.Usd, "Source");
        var destinationWallet = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd, "Destination");

        _walletRepository.Setup(r => r.GetByIdAsync(sourceWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sourceWallet);
        _walletRepository.Setup(r => r.GetByIdAsync(destinationWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(destinationWallet);
        _transactionRepository
            .Setup(r => r.FindByIdempotencyKeyAsync(ownerId, "idem-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);
        _transactionRepository
            .Setup(r => r.PostTransferIfSufficientFundsAsync(sourceWallet.Id, 5_000, It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransferPostResult(TransferPostOutcome.InsufficientFunds, null));

        var handler = CreateHandler();
        var command = new TransferCommand(ownerId, sourceWallet.Id, destinationWallet.Id, 5_000, "idem-key");

        await Assert.ThrowsAsync<InsufficientFundsException>(() => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_ConcurrentRaceOnPost_MatchingParameters_ReturnsWinnersDto()
    {
        var ownerId = Guid.NewGuid();
        var sourceWallet = LedgerAccount.Open(ownerId, Currency.Usd, "Source");
        var destinationWallet = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd, "Destination");
        var winner = BuildTransferTransaction(ownerId, sourceWallet.Id, destinationWallet.Id, 5_000, Currency.Usd);

        _walletRepository.Setup(r => r.GetByIdAsync(sourceWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sourceWallet);
        _walletRepository.Setup(r => r.GetByIdAsync(destinationWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(destinationWallet);

        _transactionRepository
            .SetupSequence(r => r.FindByIdempotencyKeyAsync(ownerId, "idem-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null)
            .ReturnsAsync(winner);

        _transactionRepository
            .Setup(r => r.PostTransferIfSufficientFundsAsync(sourceWallet.Id, 5_000, It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IdempotencyKeyAlreadyUsedException());

        var handler = CreateHandler();
        var command = new TransferCommand(ownerId, sourceWallet.Id, destinationWallet.Id, 5_000, "idem-key");

        var dto = await handler.HandleAsync(command, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(winner.Id, dto!.Id);
    }

    [Fact]
    public async Task HandleAsync_ConcurrentRaceOnPost_DifferentParameters_ThrowsConflict()
    {
        var ownerId = Guid.NewGuid();
        var sourceWallet = LedgerAccount.Open(ownerId, Currency.Usd, "Source");
        var destinationWallet = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd, "Destination");
        var winner = BuildTransferTransaction(ownerId, sourceWallet.Id, destinationWallet.Id, 2_000, Currency.Usd); // different amount

        _walletRepository.Setup(r => r.GetByIdAsync(sourceWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sourceWallet);
        _walletRepository.Setup(r => r.GetByIdAsync(destinationWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(destinationWallet);

        _transactionRepository
            .SetupSequence(r => r.FindByIdempotencyKeyAsync(ownerId, "idem-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null)
            .ReturnsAsync(winner);

        _transactionRepository
            .Setup(r => r.PostTransferIfSufficientFundsAsync(sourceWallet.Id, 5_000, It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IdempotencyKeyAlreadyUsedException());

        var handler = CreateHandler();
        var command = new TransferCommand(ownerId, sourceWallet.Id, destinationWallet.Id, 5_000, "idem-key");

        await Assert.ThrowsAsync<IdempotencyKeyConflictException>(() => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_HappyPath_PostsBalancedTransferAndReturnsDto()
    {
        var ownerId = Guid.NewGuid();
        var sourceWallet = LedgerAccount.Open(ownerId, Currency.Usd, "Source");
        var destinationWallet = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd, "Destination");

        _walletRepository.Setup(r => r.GetByIdAsync(sourceWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sourceWallet);
        _walletRepository.Setup(r => r.GetByIdAsync(destinationWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(destinationWallet);
        _transactionRepository
            .Setup(r => r.FindByIdempotencyKeyAsync(ownerId, "idem-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        Transaction? posted = null;
        _transactionRepository
            .Setup(r => r.PostTransferIfSufficientFundsAsync(sourceWallet.Id, 5_000, It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, long, Transaction, CancellationToken>((_, _, t, _) => posted = t)
            .ReturnsAsync((Guid _, long _, Transaction t, CancellationToken _) => new TransferPostResult(TransferPostOutcome.Posted, t));

        var handler = CreateHandler();
        var command = new TransferCommand(ownerId, sourceWallet.Id, destinationWallet.Id, 5_000, "idem-key");

        var dto = await handler.HandleAsync(command, CancellationToken.None);

        Assert.NotNull(posted);
        Assert.Equal(TransactionType.Transfer, posted!.Type);
        Assert.Equal(2, posted.Entries.Count);
        Assert.Contains(posted.Entries, e =>
            e.AccountId == sourceWallet.Id && e.Direction == LedgerEntryDirection.Debit &&
            e.AmountMinorUnits == 5_000 && e.Currency == Currency.Usd);
        Assert.Contains(posted.Entries, e =>
            e.AccountId == destinationWallet.Id && e.Direction == LedgerEntryDirection.Credit &&
            e.AmountMinorUnits == 5_000 && e.Currency == Currency.Usd);

        Assert.NotNull(dto);
        Assert.Equal(posted.Id, dto!.Id);
        Assert.Equal(nameof(TransactionType.Transfer), dto.Type);
        Assert.Equal(2, dto.Entries.Count);
    }
}
