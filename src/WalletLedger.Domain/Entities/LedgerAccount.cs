using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Domain.Entities;

/// <summary>
/// One per wallet. Never carries a mutable balance — balance is always derived
/// from posted LedgerEntry rows (see ARCHITECTURE.md, "The ledger is the source of truth").
/// </summary>
public sealed class LedgerAccount
{
    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Currency Currency { get; private set; }
    public string? DisplayName { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private LedgerAccount()
    {
    }

    public static LedgerAccount Open(Guid ownerUserId, Currency currency, string? displayName = null)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("A wallet must have an owner.", nameof(ownerUserId));
        }

        return new LedgerAccount
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            Currency = currency,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

}
