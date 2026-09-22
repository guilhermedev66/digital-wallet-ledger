using Moq;
using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Exceptions;
using WalletLedger.Application.Ledger;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Tests.Ledger;

public class ReverseTransactionHandlerTests
{
    private const int MaxIdempotencyKeyLength = 128;

    private readonly Mock<IWalletRepository> _walletRepository = new();
    private readonly Mock<ITransactionRepository> _transactionRepository = new();

    private ReverseTransactionHandler CreateHandler() => new(_walletRepository.Object, _transactionRepository.Object);

    // Debit-normal convention (see MEMORY.md): source/wallet-that-loses-money gets Credit,
    // destination/wallet-that-gains-money gets Debit - matches TransferHandler/SimulateFundingHandler.
    private static Transaction BuildTransferTransaction(
        Guid ownerId, Guid sourceId, Guid destinationId, long amount, Currency currency, string idempotencyKey = "original-key") =>
        Transaction.Post(
            ownerId, idempotencyKey, TransactionType.Transfer,
            [
                new LedgerEntryLine(sourceId, LedgerEntryDirection.Credit, amount, currency),
                new LedgerEntryLine(destinationId, LedgerEntryDirection.Debit, amount, currency),
            ]);

    private static Transaction BuildFundingTransaction(
        Guid ownerId, Guid walletId, Guid fundingAccountId, long amount, Currency currency, string idempotencyKey = "original-key") =>
        Transaction.Post(
            ownerId, idempotencyKey, TransactionType.SimulatedFunding,
            [
                new LedgerEntryLine(walletId, LedgerEntryDirection.Debit, amount, currency),
                new LedgerEntryLine(fundingAccountId, LedgerEntryDirection.Credit, amount, currency),
            ]);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_IdempotencyKeyMissing_ThrowsArgumentException_WithoutCallingRepository(string? idempotencyKey)
    {
        var handler = CreateHandler();
        var command = new ReverseTransactionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), idempotencyKey!);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        _walletRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_IdempotencyKeyOverMaxLength_ThrowsArgumentException_WithoutCallingRepository()
    {
        var handler = CreateHandler();
        var oversizedKey = new string('a', MaxIdempotencyKeyLength + 1);
        var command = new ReverseTransactionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), oversizedKey);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        _walletRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WalletDoesNotExist_ReturnsNull()
    {
        _walletRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((LedgerAccount?)null);

        var handler = CreateHandler();
        var command = new ReverseTransactionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "reverse-key");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_WalletBelongsToDifferentOwner_ReturnsNull()
    {
        var wallet = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd, "Someone else's wallet");
        _walletRepository.Setup(r => r.GetByIdAsync(wallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(wallet);

        var handler = CreateHandler();
        var command = new ReverseTransactionCommand(Guid.NewGuid(), wallet.Id, Guid.NewGuid(), "reverse-key");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_TransactionDoesNotExist_ReturnsNull()
    {
        var ownerId = Guid.NewGuid();
        var wallet = LedgerAccount.Open(ownerId, Currency.Usd, "Mine");
        _walletRepository.Setup(r => r.GetByIdAsync(wallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(wallet);
        _transactionRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Transaction?)null);

        var handler = CreateHandler();
        var command = new ReverseTransactionCommand(ownerId, wallet.Id, Guid.NewGuid(), "reverse-key");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_TransactionExistsButWalletWasNotAParty_ReturnsNull()
    {
        var ownerId = Guid.NewGuid();
        var wallet = LedgerAccount.Open(ownerId, Currency.Usd, "Mine");
        var unrelated = BuildTransferTransaction(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1_000, Currency.Usd);

        _walletRepository.Setup(r => r.GetByIdAsync(wallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(wallet);
        _transactionRepository.Setup(r => r.GetByIdAsync(unrelated.Id, It.IsAny<CancellationToken>())).ReturnsAsync(unrelated);

        var handler = CreateHandler();
        var command = new ReverseTransactionCommand(ownerId, wallet.Id, unrelated.Id, "reverse-key");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_TransactionIsAlreadyAReversal_ThrowsArgumentException()
    {
        var ownerId = Guid.NewGuid();
        var wallet = LedgerAccount.Open(ownerId, Currency.Usd, "Mine");
        var other = Guid.NewGuid();
        var original = BuildTransferTransaction(ownerId, wallet.Id, other, 1_000, Currency.Usd);
        var priorReversal = Transaction.Post(
            ownerId, "prior-reversal-key", TransactionType.Reversal,
            [
                new LedgerEntryLine(wallet.Id, LedgerEntryDirection.Debit, 1_000, Currency.Usd),
                new LedgerEntryLine(other, LedgerEntryDirection.Credit, 1_000, Currency.Usd),
            ],
            original.Id);

        _walletRepository.Setup(r => r.GetByIdAsync(wallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(wallet);
        _transactionRepository.Setup(r => r.GetByIdAsync(priorReversal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(priorReversal);

        var handler = CreateHandler();
        var command = new ReverseTransactionCommand(ownerId, wallet.Id, priorReversal.Id, "reverse-key");

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_AlreadyReversedByAnotherKey_ThrowsTransactionAlreadyReversedException_WithoutPosting()
    {
        var ownerId = Guid.NewGuid();
        var sourceWallet = LedgerAccount.Open(ownerId, Currency.Usd, "Source");
        var destinationWallet = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd, "Destination");
        var original = BuildTransferTransaction(ownerId, sourceWallet.Id, destinationWallet.Id, 1_000, Currency.Usd);
        var existingReversal = Transaction.Post(
            ownerId, "someone-elses-key", TransactionType.Reversal,
            [
                new LedgerEntryLine(sourceWallet.Id, LedgerEntryDirection.Debit, 1_000, Currency.Usd),
                new LedgerEntryLine(destinationWallet.Id, LedgerEntryDirection.Credit, 1_000, Currency.Usd),
            ],
            original.Id);

        _walletRepository.Setup(r => r.GetByIdAsync(sourceWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sourceWallet);
        _walletRepository.Setup(r => r.GetByIdAsync(destinationWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(destinationWallet);
        _transactionRepository.Setup(r => r.GetByIdAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        _transactionRepository
            .Setup(r => r.FindByIdempotencyKeyAsync(ownerId, "reverse-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);
        _transactionRepository.Setup(r => r.FindReversalOfAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existingReversal);

        var handler = CreateHandler();
        // The sender isn't the account the reversal would debit (the destination is) - admin
        // needed, see the authorization-gate tests below for the non-admin case.
        var command = new ReverseTransactionCommand(ownerId, sourceWallet.Id, original.Id, "reverse-key", CallerIsAdmin: true);

        await Assert.ThrowsAsync<TransactionAlreadyReversedException>(() => handler.HandleAsync(command, CancellationToken.None));

        _transactionRepository.Verify(
            r => r.PostTransferIfSufficientFundsAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<Transaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_IdempotencyKeyAlreadyUsedWithMatchingParameters_ReturnsExistingDto_WithoutPosting()
    {
        var ownerId = Guid.NewGuid();
        var sourceWallet = LedgerAccount.Open(ownerId, Currency.Usd, "Source");
        var destinationWallet = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd, "Destination");
        var original = BuildTransferTransaction(ownerId, sourceWallet.Id, destinationWallet.Id, 1_000, Currency.Usd);
        var existingReversal = Transaction.Post(
            ownerId, "reverse-key", TransactionType.Reversal,
            [
                new LedgerEntryLine(sourceWallet.Id, LedgerEntryDirection.Debit, 1_000, Currency.Usd),
                new LedgerEntryLine(destinationWallet.Id, LedgerEntryDirection.Credit, 1_000, Currency.Usd),
            ],
            original.Id);

        _walletRepository.Setup(r => r.GetByIdAsync(sourceWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sourceWallet);
        _walletRepository.Setup(r => r.GetByIdAsync(destinationWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(destinationWallet);
        _transactionRepository.Setup(r => r.GetByIdAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        _transactionRepository
            .Setup(r => r.FindByIdempotencyKeyAsync(ownerId, "reverse-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingReversal);

        var handler = CreateHandler();
        var command = new ReverseTransactionCommand(ownerId, sourceWallet.Id, original.Id, "reverse-key", CallerIsAdmin: true);

        var dto = await handler.HandleAsync(command, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(existingReversal.Id, dto!.Id);
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
        var original = BuildTransferTransaction(ownerId, sourceWallet.Id, destinationWallet.Id, 1_000, Currency.Usd);
        // Reuses "reverse-key" for a reversal of an unrelated transaction - different parameters.
        var unrelatedReversal = Transaction.Post(
            ownerId, "reverse-key", TransactionType.Reversal,
            [
                new LedgerEntryLine(sourceWallet.Id, LedgerEntryDirection.Debit, 250, Currency.Usd),
                new LedgerEntryLine(Guid.NewGuid(), LedgerEntryDirection.Credit, 250, Currency.Usd),
            ],
            Guid.NewGuid());

        _walletRepository.Setup(r => r.GetByIdAsync(sourceWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sourceWallet);
        _walletRepository.Setup(r => r.GetByIdAsync(destinationWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(destinationWallet);
        _transactionRepository.Setup(r => r.GetByIdAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        _transactionRepository
            .Setup(r => r.FindByIdempotencyKeyAsync(ownerId, "reverse-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(unrelatedReversal);

        var handler = CreateHandler();
        var command = new ReverseTransactionCommand(ownerId, sourceWallet.Id, original.Id, "reverse-key", CallerIsAdmin: true);

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
        var original = BuildTransferTransaction(ownerId, sourceWallet.Id, destinationWallet.Id, 1_000, Currency.Usd);

        _walletRepository.Setup(r => r.GetByIdAsync(sourceWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sourceWallet);
        _walletRepository.Setup(r => r.GetByIdAsync(destinationWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(destinationWallet);
        _transactionRepository.Setup(r => r.GetByIdAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        _transactionRepository
            .Setup(r => r.FindByIdempotencyKeyAsync(ownerId, "reverse-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);
        _transactionRepository.Setup(r => r.FindReversalOfAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Transaction?)null);
        // Reversing this transfer needs the DESTINATION's balance (it received the Debit) - it
        // has since been spent elsewhere, so the reversal can't be covered.
        _transactionRepository
            .Setup(r => r.PostTransferIfSufficientFundsAsync(destinationWallet.Id, 1_000, It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransferPostResult(TransferPostOutcome.InsufficientFunds, null));

        var handler = CreateHandler();
        var command = new ReverseTransactionCommand(ownerId, sourceWallet.Id, original.Id, "reverse-key", CallerIsAdmin: true);

        await Assert.ThrowsAsync<InsufficientFundsException>(() => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_ReverseTransfer_ChecksDestinationBalance_PostsMirroredEntries()
    {
        var ownerId = Guid.NewGuid();
        var sourceWallet = LedgerAccount.Open(ownerId, Currency.Usd, "Source");
        var destinationWallet = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd, "Destination");
        var original = BuildTransferTransaction(ownerId, sourceWallet.Id, destinationWallet.Id, 5_000, Currency.Usd);

        _walletRepository.Setup(r => r.GetByIdAsync(sourceWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sourceWallet);
        _walletRepository.Setup(r => r.GetByIdAsync(destinationWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(destinationWallet);
        _transactionRepository.Setup(r => r.GetByIdAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        _transactionRepository
            .Setup(r => r.FindByIdempotencyKeyAsync(ownerId, "reverse-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);
        _transactionRepository.Setup(r => r.FindReversalOfAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Transaction?)null);

        Transaction? posted = null;
        _transactionRepository
            .Setup(r => r.PostTransferIfSufficientFundsAsync(destinationWallet.Id, 5_000, It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, long, Transaction, CancellationToken>((_, _, t, _) => posted = t)
            .ReturnsAsync((Guid _, long _, Transaction t, CancellationToken _) => new TransferPostResult(TransferPostOutcome.Posted, t));

        var handler = CreateHandler();
        // Sender-initiated - the reversal debits the destination, not the caller's own wallet,
        // so this only works because CallerIsAdmin is set (see the authorization-gate tests below).
        var command = new ReverseTransactionCommand(ownerId, sourceWallet.Id, original.Id, "reverse-key", CallerIsAdmin: true);

        var dto = await handler.HandleAsync(command, CancellationToken.None);

        Assert.NotNull(posted);
        Assert.Equal(TransactionType.Reversal, posted!.Type);
        Assert.Equal(original.Id, posted.ReversalOfTransactionId);
        Assert.Equal(2, posted.Entries.Count);
        Assert.Contains(posted.Entries, e =>
            e.AccountId == sourceWallet.Id && e.Direction == LedgerEntryDirection.Debit && e.AmountMinorUnits == 5_000);
        Assert.Contains(posted.Entries, e =>
            e.AccountId == destinationWallet.Id && e.Direction == LedgerEntryDirection.Credit && e.AmountMinorUnits == 5_000);

        Assert.NotNull(dto);
        Assert.Equal(posted.Id, dto!.Id);
        Assert.Equal(nameof(TransactionType.Reversal), dto.Type);
        Assert.Equal(original.Id, dto.ReversalOfTransactionId);
    }

    [Fact]
    public async Task HandleAsync_ReverseSimulatedFunding_ChecksWalletBalance_PostsMirroredEntries()
    {
        var ownerId = Guid.NewGuid();
        var wallet = LedgerAccount.Open(ownerId, Currency.Usd, "Mine");
        var fundingAccount = LedgerAccount.OpenSystemFundingAccount(Currency.Usd, "USD Funding Source");
        var original = BuildFundingTransaction(ownerId, wallet.Id, fundingAccount.Id, 2_000, Currency.Usd);

        _walletRepository.Setup(r => r.GetByIdAsync(wallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(wallet);
        _transactionRepository.Setup(r => r.GetByIdAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        _transactionRepository
            .Setup(r => r.FindByIdempotencyKeyAsync(ownerId, "reverse-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);
        _transactionRepository.Setup(r => r.FindReversalOfAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Transaction?)null);

        Transaction? posted = null;
        _transactionRepository
            .Setup(r => r.PostTransferIfSufficientFundsAsync(wallet.Id, 2_000, It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, long, Transaction, CancellationToken>((_, _, t, _) => posted = t)
            .ReturnsAsync((Guid _, long _, Transaction t, CancellationToken _) => new TransferPostResult(TransferPostOutcome.Posted, t));

        var handler = CreateHandler();
        var command = new ReverseTransactionCommand(ownerId, wallet.Id, original.Id, "reverse-key");

        var dto = await handler.HandleAsync(command, CancellationToken.None);

        Assert.NotNull(posted);
        Assert.Equal(TransactionType.Reversal, posted!.Type);
        Assert.Contains(posted.Entries, e =>
            e.AccountId == wallet.Id && e.Direction == LedgerEntryDirection.Credit && e.AmountMinorUnits == 2_000);
        Assert.Contains(posted.Entries, e =>
            e.AccountId == fundingAccount.Id && e.Direction == LedgerEntryDirection.Debit && e.AmountMinorUnits == 2_000);
        Assert.NotNull(dto);
    }

    [Fact]
    public async Task HandleAsync_ConcurrentRaceOnPost_MatchingParameters_ReturnsWinnersDto()
    {
        var ownerId = Guid.NewGuid();
        var sourceWallet = LedgerAccount.Open(ownerId, Currency.Usd, "Source");
        var destinationWallet = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd, "Destination");
        var original = BuildTransferTransaction(ownerId, sourceWallet.Id, destinationWallet.Id, 5_000, Currency.Usd);
        var winner = Transaction.Post(
            ownerId, "reverse-key", TransactionType.Reversal,
            [
                new LedgerEntryLine(sourceWallet.Id, LedgerEntryDirection.Debit, 5_000, Currency.Usd),
                new LedgerEntryLine(destinationWallet.Id, LedgerEntryDirection.Credit, 5_000, Currency.Usd),
            ],
            original.Id);

        _walletRepository.Setup(r => r.GetByIdAsync(sourceWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sourceWallet);
        _walletRepository.Setup(r => r.GetByIdAsync(destinationWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(destinationWallet);
        _transactionRepository.Setup(r => r.GetByIdAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        _transactionRepository.Setup(r => r.FindReversalOfAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Transaction?)null);

        _transactionRepository
            .SetupSequence(r => r.FindByIdempotencyKeyAsync(ownerId, "reverse-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null)
            .ReturnsAsync(winner);

        _transactionRepository
            .Setup(r => r.PostTransferIfSufficientFundsAsync(destinationWallet.Id, 5_000, It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IdempotencyKeyAlreadyUsedException());

        var handler = CreateHandler();
        var command = new ReverseTransactionCommand(ownerId, sourceWallet.Id, original.Id, "reverse-key", CallerIsAdmin: true);

        var dto = await handler.HandleAsync(command, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(winner.Id, dto!.Id);
    }

    [Fact]
    public async Task HandleAsync_TransferReversalBySender_NonAdmin_ReturnsNull()
    {
        // The reversal would debit the DESTINATION wallet (it received the Debit), not the
        // sender's own - a non-admin sender can't unilaterally claw money back out of someone
        // else's wallet. Same non-leak pattern as everywhere else: 404, not 403.
        var ownerId = Guid.NewGuid();
        var sourceWallet = LedgerAccount.Open(ownerId, Currency.Usd, "Source");
        var destinationWallet = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd, "Destination");
        var original = BuildTransferTransaction(ownerId, sourceWallet.Id, destinationWallet.Id, 5_000, Currency.Usd);

        _walletRepository.Setup(r => r.GetByIdAsync(sourceWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sourceWallet);
        _walletRepository.Setup(r => r.GetByIdAsync(destinationWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(destinationWallet);
        _transactionRepository.Setup(r => r.GetByIdAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(original);

        var handler = CreateHandler();
        var command = new ReverseTransactionCommand(ownerId, sourceWallet.Id, original.Id, "reverse-key", CallerIsAdmin: false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Null(result);
        _transactionRepository.Verify(
            r => r.PostTransferIfSufficientFundsAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<Transaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TransferReversalByRecipient_NonAdmin_Succeeds()
    {
        // The recipient voluntarily sending the money back only debits their OWN wallet - no
        // admin needed, this is self-service, same as reversing your own SimulatedFunding.
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var sourceWallet = LedgerAccount.Open(senderId, Currency.Usd, "Source");
        var destinationWallet = LedgerAccount.Open(recipientId, Currency.Usd, "Destination");
        var original = BuildTransferTransaction(senderId, sourceWallet.Id, destinationWallet.Id, 5_000, Currency.Usd);

        _walletRepository.Setup(r => r.GetByIdAsync(destinationWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(destinationWallet);
        _transactionRepository.Setup(r => r.GetByIdAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        _transactionRepository
            .Setup(r => r.FindByIdempotencyKeyAsync(recipientId, "reverse-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);
        _transactionRepository.Setup(r => r.FindReversalOfAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Transaction?)null);
        _transactionRepository
            .Setup(r => r.PostTransferIfSufficientFundsAsync(destinationWallet.Id, 5_000, It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, long _, Transaction t, CancellationToken _) => new TransferPostResult(TransferPostOutcome.Posted, t));

        var handler = CreateHandler();
        // Recipient initiates via their OWN wallet route, no CallerIsAdmin needed.
        var command = new ReverseTransactionCommand(recipientId, destinationWallet.Id, original.Id, "reverse-key");

        var dto = await handler.HandleAsync(command, CancellationToken.None);

        Assert.NotNull(dto);
    }

    [Fact]
    public async Task HandleAsync_Admin_CanReverseTransferBetweenTwoOtherUsers_NeitherWalletIsTheAdminsOwn()
    {
        // Security-gate finding (M7 full pass): an admin must be able to intervene on a
        // transfer between two OTHER users - that's the documented purpose of the admin
        // bypass - not just on transactions where they happen to own one of the wallets. Both
        // the route wallet ownership check AND the affected-account check need to honor
        // CallerIsAdmin, not just the second one.
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var sourceWallet = LedgerAccount.Open(senderId, Currency.Usd, "Source");
        var destinationWallet = LedgerAccount.Open(recipientId, Currency.Usd, "Destination");
        var original = BuildTransferTransaction(senderId, sourceWallet.Id, destinationWallet.Id, 5_000, Currency.Usd);

        _walletRepository.Setup(r => r.GetByIdAsync(sourceWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sourceWallet);
        _walletRepository.Setup(r => r.GetByIdAsync(destinationWallet.Id, It.IsAny<CancellationToken>())).ReturnsAsync(destinationWallet);
        _transactionRepository.Setup(r => r.GetByIdAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        _transactionRepository
            .Setup(r => r.FindByIdempotencyKeyAsync(adminId, "reverse-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);
        _transactionRepository.Setup(r => r.FindReversalOfAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Transaction?)null);

        Transaction? posted = null;
        _transactionRepository
            .Setup(r => r.PostTransferIfSufficientFundsAsync(destinationWallet.Id, 5_000, It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, long, Transaction, CancellationToken>((_, _, t, _) => posted = t)
            .ReturnsAsync((Guid _, long _, Transaction t, CancellationToken _) => new TransferPostResult(TransferPostOutcome.Posted, t));

        var handler = CreateHandler();
        // Admin routes through the SENDER's wallet (neither wallet is the admin's own).
        var command = new ReverseTransactionCommand(adminId, sourceWallet.Id, original.Id, "reverse-key", CallerIsAdmin: true);

        var dto = await handler.HandleAsync(command, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.NotNull(posted);
        Assert.Equal(adminId, posted!.RequestedByUserId);
    }
}
