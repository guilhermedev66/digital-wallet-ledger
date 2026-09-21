using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Domain.Entities;

public enum LedgerEntryDirection
{
    Debit = 0,
    Credit = 1,
}

/// <summary>
/// Immutable row, never updated or deleted after insert. The only way to create one is
/// through Transaction.Post - there is no public constructor path, so a LedgerEntry can
/// never exist outside of a balance-checked Transaction.
/// </summary>
public sealed class LedgerEntry
{
    public Guid Id { get; private set; }
    public Guid TransactionId { get; private set; }
    public Guid AccountId { get; private set; }
    public LedgerEntryDirection Direction { get; private set; }
    public long AmountMinorUnits { get; private set; }
    public Currency Currency { get; private set; }

    private LedgerEntry()
    {
    }

    internal static LedgerEntry Create(Guid transactionId, Guid accountId, LedgerEntryDirection direction, long amountMinorUnits, Currency currency)
    {
        if (amountMinorUnits <= 0)
        {
            throw new ArgumentException("A ledger entry amount must be a positive number of minor units.", nameof(amountMinorUnits));
        }

        return new LedgerEntry
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            AccountId = accountId,
            Direction = direction,
            AmountMinorUnits = amountMinorUnits,
            Currency = currency,
        };
    }
}
