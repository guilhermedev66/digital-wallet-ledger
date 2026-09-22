using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Dtos;
using WalletLedger.Application.Exceptions;
using WalletLedger.Application.Mapping;
using WalletLedger.Domain.Entities;

namespace WalletLedger.Application.Ledger;

/// <summary>OwnerUserId must be resolved server-side from the authenticated caller's claims - never from client input. WalletId scopes which transaction the caller is allowed to reverse (it must be a party to it), it need not be the account actually debited by the reversal. CallerIsAdmin bypasses BOTH ownership checks below (this wallet, and the affected account) - see HandleAsync's doc comment.</summary>
public sealed record ReverseTransactionCommand(Guid OwnerUserId, Guid WalletId, Guid TransactionId, string IdempotencyKey, bool CallerIsAdmin = false);

/// <summary>
/// Posts a compensating Transaction (Type = Reversal, ReversalOfTransactionId set) whose
/// entries are the exact mirror image of the original - see ARCHITECTURE.md's "Reversals"
/// section. Never edits or deletes the original's posted entries. Built through the same
/// Transaction.Post construction path every other transaction type uses, so the balance
/// invariant is enforced the same structural way (no second, looser construction path).
/// </summary>
public sealed class ReverseTransactionHandler(IWalletRepository walletRepository, ITransactionRepository transactionRepository)
{
    private const int MaxIdempotencyKeyLength = 128; // matches the Transactions.IdempotencyKey column

    /// <summary>
    /// Returns null when the wallet doesn't exist, the transaction doesn't exist or doesn't
    /// have an entry against that wallet, or (non-admin only) the caller doesn't own that
    /// wallet OR doesn't own the account the reversal would actually debit - same non-leak
    /// pattern as GetWalletByIdHandler throughout, and the SAME admin bypass on both checks
    /// (an admin isn't required to personally own either side). The second check matters for
    /// non-admins specifically: a transfer's SENDER is a party to the transaction, but
    /// reversing it debits the RECIPIENT's wallet - letting the sender unilaterally trigger
    /// that would claw money out of someone else's wallet without their consent. So
    /// self-service reversal is only allowed when it debits the caller's own wallet (the
    /// recipient voluntarily sending it back, or a wallet owner undoing their own
    /// SimulatedFunding); reversing a transaction the *other* way - including intervening on a
    /// transfer between two OTHER users, neither of them the caller - needs the admin role.
    /// Reversing a Reversal, or a transaction that's already been reversed, or one the debited
    /// account can't cover, are all legitimate distinct outcomes instead (400/409/422 - see
    /// each exception's own doc comment).
    /// </summary>
    public async Task<TransactionDto?> HandleAsync(ReverseTransactionCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > MaxIdempotencyKeyLength)
        {
            throw new ArgumentException($"Idempotency key must be 1-{MaxIdempotencyKeyLength} characters long.", nameof(command));
        }

        var wallet = await walletRepository.GetByIdAsync(command.WalletId, ct);
        if (wallet is null || (wallet.OwnerUserId != command.OwnerUserId && !command.CallerIsAdmin))
        {
            return null;
        }

        var original = await transactionRepository.GetByIdAsync(command.TransactionId, ct);
        if (original is null || original.Entries.All(e => e.AccountId != wallet.Id))
        {
            return null;
        }

        if (original.Type == TransactionType.Reversal)
        {
            throw new ArgumentException("Cannot reverse a reversal transaction.", nameof(command));
        }

        // Balance is debit-normal for every LedgerAccount (see MEMORY.md): a Debit entry
        // increases an account's derived balance, Credit decreases it. Mirroring flips each
        // entry's direction, so the account that LOSES money in the reversal - the one that
        // needs a balance floor check, since it can genuinely have since been spent below the
        // amount being clawed back - is whichever account held the DEBIT entry in the original
        // (its reversal entry is Credit), not the credited one. Concretely: reversing a Transfer
        // checks the original destination (it received the Debit); reversing a SimulatedFunding
        // checks the wallet (it received the Debit) - in both cases that's always the UserWallet
        // side, never SystemFunding, for every transaction type this project builds today. Every
        // transaction type here has exactly one Debit entry - see MEMORY.md's note on
        // PostTransferIfSufficientFundsAsync locking only a single account for why supporting
        // more than one here would need real revisiting, not just a loop.
        var debitedInOriginalAccountIds = original.Entries.Where(e => e.Direction == LedgerEntryDirection.Debit).Select(e => e.AccountId).Distinct().ToList();
        if (debitedInOriginalAccountIds.Count != 1)
        {
            throw new InvalidOperationException("Reversal of a transaction with more than one debited account is not supported.");
        }

        var accountToCheck = original.Entries.First(e => e.Direction == LedgerEntryDirection.Debit);

        var affectedAccount = await walletRepository.GetByIdAsync(accountToCheck.AccountId, ct)
            ?? throw new InvalidOperationException("A posted ledger entry referenced an account that no longer exists.");

        if (affectedAccount.OwnerUserId != command.OwnerUserId && !command.CallerIsAdmin)
        {
            return null;
        }

        var existing = await transactionRepository.FindByIdempotencyKeyAsync(command.OwnerUserId, command.IdempotencyKey, ct);
        if (existing is not null)
        {
            return MatchesThisReversal(existing, original) ? existing.ToDto() : throw new IdempotencyKeyConflictException();
        }

        // App-level pre-check for the common case - the (ReversalOfTransactionId) unique
        // constraint below is the real guarantee against a concurrent second reversal attempt
        // (a different idempotency key) racing this one, same two-layer pattern as every other
        // idempotency check in this project (see MEMORY.md).
        var alreadyReversed = await transactionRepository.FindReversalOfAsync(original.Id, ct);
        if (alreadyReversed is not null)
        {
            throw new TransactionAlreadyReversedException();
        }

        var reversalLines = original.Entries
            .Select(e => new LedgerEntryLine(e.AccountId, Mirror(e.Direction), e.AmountMinorUnits, e.Currency))
            .ToList();

        var reversal = Transaction.Post(command.OwnerUserId, command.IdempotencyKey, TransactionType.Reversal, reversalLines, original.Id);

        try
        {
            var result = await transactionRepository.PostTransferIfSufficientFundsAsync(accountToCheck.AccountId, accountToCheck.AmountMinorUnits, reversal, ct);

            if (result.Outcome == TransferPostOutcome.InsufficientFunds)
            {
                throw new InsufficientFundsException();
            }

            return result.Transaction!.ToDto();
        }
        catch (IdempotencyKeyAlreadyUsedException)
        {
            // A concurrent request for the same key won the race between our pre-check above
            // and this insert attempt - the DB constraint is the real guarantee.
            var raced = await transactionRepository.FindByIdempotencyKeyAsync(command.OwnerUserId, command.IdempotencyKey, ct)
                ?? throw new InvalidOperationException("Idempotency conflict reported but no matching transaction was found on re-read.");

            return MatchesThisReversal(raced, original) ? raced.ToDto() : throw new IdempotencyKeyConflictException();
        }
    }

    private static LedgerEntryDirection Mirror(LedgerEntryDirection direction) =>
        direction == LedgerEntryDirection.Debit ? LedgerEntryDirection.Credit : LedgerEntryDirection.Debit;

    private static bool MatchesThisReversal(Transaction candidate, Transaction original) =>
        candidate.Type == TransactionType.Reversal
        && candidate.ReversalOfTransactionId == original.Id
        && original.Entries.All(e => candidate.HasMatchingEntry(e.AccountId, Mirror(e.Direction), e.AmountMinorUnits, e.Currency));
}
