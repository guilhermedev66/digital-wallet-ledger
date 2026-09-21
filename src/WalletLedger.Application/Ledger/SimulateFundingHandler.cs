using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Dtos;
using WalletLedger.Application.Mapping;
using WalletLedger.Domain.Entities;

namespace WalletLedger.Application.Ledger;

/// <summary>OwnerUserId must be resolved server-side from the authenticated caller's claims - never from client input.</summary>
public sealed record SimulateFundingCommand(Guid OwnerUserId, Guid WalletId, long AmountMinorUnits, string IdempotencyKey);

/// <summary>
/// Demo-only: credits a user's wallet from the system's SystemFunding account for that
/// currency. Never a real payment processor or real money (see ARCHITECTURE.md, "What this
/// project deliberately does not do").
/// </summary>
public sealed class SimulateFundingHandler(IWalletRepository walletRepository, ITransactionRepository transactionRepository)
{
    private const long MaxAmountMinorUnits = 1_000_000_00; // 1,000,000.00 - a sane demo ceiling, not a real limit
    private const int MaxIdempotencyKeyLength = 128; // matches the Transactions.IdempotencyKey column

    /// <summary>
    /// Returns null both when the wallet doesn't exist and when it belongs to someone else -
    /// same non-leak pattern as GetWalletByIdHandler (see that handler for why: a 403 here
    /// would itself reveal the wallet exists).
    /// </summary>
    public async Task<TransactionDto?> HandleAsync(SimulateFundingCommand command, CancellationToken ct)
    {
        if (command.AmountMinorUnits is <= 0 or > MaxAmountMinorUnits)
        {
            throw new ArgumentException($"Amount must be between 1 and {MaxAmountMinorUnits} minor units.", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > MaxIdempotencyKeyLength)
        {
            throw new ArgumentException($"Idempotency key must be 1-{MaxIdempotencyKeyLength} characters long.", nameof(command));
        }

        var wallet = await walletRepository.GetByIdAsync(command.WalletId, ct);
        if (wallet is null || wallet.OwnerUserId != command.OwnerUserId)
        {
            return null;
        }

        var fundingAccount = await walletRepository.GetSystemFundingAccountAsync(wallet.Currency, ct)
            ?? throw new InvalidOperationException($"No system funding account is configured for currency '{wallet.Currency}'.");

        var transaction = Transaction.Post(
            command.OwnerUserId,
            command.IdempotencyKey,
            TransactionType.SimulatedFunding,
            [
                new LedgerEntryLine(wallet.Id, LedgerEntryDirection.Debit, command.AmountMinorUnits, wallet.Currency),
                new LedgerEntryLine(fundingAccount.Id, LedgerEntryDirection.Credit, command.AmountMinorUnits, wallet.Currency),
            ]);

        await transactionRepository.AddAsync(transaction, ct);

        return transaction.ToDto();
    }
}
