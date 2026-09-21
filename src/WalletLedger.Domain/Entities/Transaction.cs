namespace WalletLedger.Domain.Entities;

public enum TransactionType
{
    Transfer = 0,
    SimulatedFunding = 1,
    Reversal = 2,
}

/// <summary>
/// An immutable, atomically-posted group of LedgerEntry rows (see ARCHITECTURE.md, "The
/// ledger is the source of truth"). The only way to construct one is Post, which makes it
/// structurally impossible to end up with an unbalanced transaction - there is no other
/// path (public constructor, setter, or Add method) that could ever produce one.
/// </summary>
public sealed class Transaction
{
    public Guid Id { get; private set; }

    /// <summary>Who initiated this transaction/funding command - not necessarily a party to every entry (e.g. a future admin-triggered reversal).</summary>
    public Guid RequestedByUserId { get; private set; }

    /// <summary>
    /// Client-supplied, carried for future replay-detection (see ARCHITECTURE.md's
    /// (RequestedByUserId, IdempotencyKey) unique constraint) - that constraint and its
    /// replay test are M3 scope; this milestone only stores the value.
    /// </summary>
    public string IdempotencyKey { get; private set; } = null!;

    public TransactionType Type { get; private set; }
    public DateTime PostedAtUtc { get; private set; }
    public Guid? ReversalOfTransactionId { get; private set; }

    private readonly List<LedgerEntry> _entries = [];
    public IReadOnlyList<LedgerEntry> Entries => _entries;

    private Transaction()
    {
    }

    public static Transaction Post(
        Guid requestedByUserId,
        string idempotencyKey,
        TransactionType type,
        IReadOnlyCollection<LedgerEntryLine> lines,
        Guid? reversalOfTransactionId = null)
    {
        if (requestedByUserId == Guid.Empty)
        {
            throw new ArgumentException("A transaction must have a requesting user.", nameof(requestedByUserId));
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("A transaction must carry an idempotency key.", nameof(idempotencyKey));
        }

        if (type == TransactionType.Reversal && reversalOfTransactionId is null)
        {
            throw new ArgumentException("A reversal transaction must reference the transaction it reverses.", nameof(reversalOfTransactionId));
        }

        if (type != TransactionType.Reversal && reversalOfTransactionId is not null)
        {
            throw new ArgumentException("Only a reversal transaction may reference another transaction.", nameof(reversalOfTransactionId));
        }

        if (lines is null || lines.Count == 0)
        {
            throw new ArgumentException("A transaction must have at least one ledger entry.", nameof(lines));
        }

        var currency = lines.First().Currency;
        if (lines.Any(line => line.Currency != currency))
        {
            throw new ArgumentException("All ledger entries in a transaction must share the same currency.", nameof(lines));
        }

        long totalDebits = 0;
        long totalCredits = 0;

        try
        {
            checked
            {
                foreach (var line in lines)
                {
                    // LedgerEntry.Create (below) also rejects a non-positive amount - checked
                    // here too so the sums below can't be corrupted by a bad value first.
                    if (line.AmountMinorUnits <= 0)
                    {
                        throw new ArgumentException("A ledger entry amount must be a positive number of minor units.", nameof(lines));
                    }

                    if (line.Direction == LedgerEntryDirection.Debit)
                    {
                        totalDebits += line.AmountMinorUnits;
                    }
                    else
                    {
                        totalCredits += line.AmountMinorUnits;
                    }
                }
            }
        }
        catch (OverflowException ex)
        {
            // Callers only ever see ArgumentException from Post - an OverflowException
            // escaping here would be an undocumented, inconsistent exception type for what
            // is still fundamentally invalid input (amounts too large to sum safely).
            throw new ArgumentException("Ledger entry amounts are too large to sum safely.", nameof(lines), ex);
        }

        if (totalDebits == 0 || totalCredits == 0)
        {
            throw new ArgumentException("A transaction must have at least one debit entry and at least one credit entry.", nameof(lines));
        }

        if (totalDebits != totalCredits)
        {
            throw new ArgumentException(
                $"Unbalanced transaction: total debits ({totalDebits}) must equal total credits ({totalCredits}).", nameof(lines));
        }

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = requestedByUserId,
            IdempotencyKey = idempotencyKey,
            Type = type,
            PostedAtUtc = DateTime.UtcNow,
            ReversalOfTransactionId = reversalOfTransactionId,
        };

        foreach (var line in lines)
        {
            transaction._entries.Add(LedgerEntry.Create(transaction.Id, line.AccountId, line.Direction, line.AmountMinorUnits, line.Currency));
        }

        return transaction;
    }
}
