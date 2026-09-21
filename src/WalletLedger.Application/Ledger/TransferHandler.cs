using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Dtos;
using WalletLedger.Application.Exceptions;
using WalletLedger.Application.Mapping;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Ledger;

/// <summary>OwnerUserId must be resolved server-side from the authenticated caller's claims - never from client input.</summary>
public sealed record TransferCommand(Guid OwnerUserId, Guid SourceWalletId, Guid DestinationWalletId, long AmountMinorUnits, string IdempotencyKey);

public sealed class TransferHandler(IWalletRepository walletRepository, ITransactionRepository transactionRepository)
{
    private const long MaxAmountMinorUnits = 1_000_000_00; // matches SimulateFundingHandler's demo ceiling
    private const int MaxIdempotencyKeyLength = 128; // matches the Transactions.IdempotencyKey column

    /// <summary>
    /// Returns null when the source wallet doesn't exist or isn't the caller's - same
    /// non-leak pattern as GetWalletByIdHandler. Everything else (bad destination, currency
    /// mismatch, self-transfer, insufficient funds, idempotency conflict) is a thrown
    /// exception, since none of those are about hiding whether a resource the caller doesn't
    /// own exists - see each exception type's own doc comment for why.
    /// </summary>
    public async Task<TransactionDto?> HandleAsync(TransferCommand command, CancellationToken ct)
    {
        if (command.AmountMinorUnits is <= 0 or > MaxAmountMinorUnits)
        {
            throw new ArgumentException($"Amount must be between 1 and {MaxAmountMinorUnits} minor units.", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > MaxIdempotencyKeyLength)
        {
            throw new ArgumentException($"Idempotency key must be 1-{MaxIdempotencyKeyLength} characters long.", nameof(command));
        }

        if (command.SourceWalletId == command.DestinationWalletId)
        {
            throw new ArgumentException("Cannot transfer a wallet to itself.", nameof(command));
        }

        var sourceWallet = await walletRepository.GetByIdAsync(command.SourceWalletId, ct);
        if (sourceWallet is null || sourceWallet.OwnerUserId != command.OwnerUserId)
        {
            return null;
        }

        var destinationWallet = await walletRepository.GetByIdAsync(command.DestinationWalletId, ct);
        if (destinationWallet is null || destinationWallet.Type != LedgerAccountType.UserWallet)
        {
            // Unlike the source wallet, the destination isn't a resource the caller owns or is
            // probing ownership of - it's a recipient identifier they're expected to already
            // know (like a bank account number). Telling them it's wrong is legitimate
            // feedback, the same way real transfer UX does, not an existence leak.
            throw new ArgumentException("Destination wallet not found.", nameof(command));
        }

        if (destinationWallet.Currency != sourceWallet.Currency)
        {
            throw new ArgumentException("Source and destination wallets must use the same currency - no cross-currency transfers.", nameof(command));
        }

        var existing = await transactionRepository.FindByIdempotencyKeyAsync(command.OwnerUserId, command.IdempotencyKey, ct);
        if (existing is not null)
        {
            return MatchesThisTransfer(existing, sourceWallet.Id, destinationWallet.Id, command.AmountMinorUnits, sourceWallet.Currency)
                ? existing.ToDto()
                : throw new IdempotencyKeyConflictException();
        }

        var transaction = Transaction.Post(
            command.OwnerUserId,
            command.IdempotencyKey,
            TransactionType.Transfer,
            [
                new LedgerEntryLine(sourceWallet.Id, LedgerEntryDirection.Debit, command.AmountMinorUnits, sourceWallet.Currency),
                new LedgerEntryLine(destinationWallet.Id, LedgerEntryDirection.Credit, command.AmountMinorUnits, sourceWallet.Currency),
            ]);

        try
        {
            var result = await transactionRepository.PostTransferIfSufficientFundsAsync(sourceWallet.Id, command.AmountMinorUnits, transaction, ct);

            if (result.Outcome == TransferPostOutcome.InsufficientFunds)
            {
                throw new InsufficientFundsException();
            }

            return result.Transaction!.ToDto();
        }
        catch (IdempotencyKeyAlreadyUsedException)
        {
            // A concurrent request for the same key won the race between our pre-check above
            // and this insert attempt - the DB constraint is the real guarantee, the pre-check
            // was just an optimization. Re-read and apply the same match-or-conflict decision.
            var raced = await transactionRepository.FindByIdempotencyKeyAsync(command.OwnerUserId, command.IdempotencyKey, ct)
                ?? throw new InvalidOperationException("Idempotency conflict reported but no matching transaction was found on re-read.");

            return MatchesThisTransfer(raced, sourceWallet.Id, destinationWallet.Id, command.AmountMinorUnits, sourceWallet.Currency)
                ? raced.ToDto()
                : throw new IdempotencyKeyConflictException();
        }
    }

    private static bool MatchesThisTransfer(Transaction existing, Guid sourceWalletId, Guid destinationWalletId, long amountMinorUnits, Currency currency) =>
        existing.Type == TransactionType.Transfer
        && existing.HasMatchingEntry(sourceWalletId, LedgerEntryDirection.Debit, amountMinorUnits, currency)
        && existing.HasMatchingEntry(destinationWalletId, LedgerEntryDirection.Credit, amountMinorUnits, currency);
}
