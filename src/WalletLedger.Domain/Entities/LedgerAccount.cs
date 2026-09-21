using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Domain.Entities;

public enum LedgerAccountType
{
    UserWallet = 0,
    SystemFunding = 1,
}

/// <summary>
/// One per wallet, plus one per internal system account (e.g. a SystemFunding source used
/// for demo deposits — see ARCHITECTURE.md "Core model"). Never carries a mutable balance —
/// balance is always derived from posted LedgerEntry rows ("The ledger is the source of truth").
/// </summary>
public sealed class LedgerAccount
{
    public Guid Id { get; private set; }

    /// <summary>Null only for SystemFunding accounts - every UserWallet account has an owner.</summary>
    public Guid? OwnerUserId { get; private set; }

    public LedgerAccountType Type { get; private set; }
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
            Type = LedgerAccountType.UserWallet,
            Currency = currency,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// One per currency, created out-of-band at startup/seed time (see Program.cs) - not
    /// reachable through any client-facing endpoint. Its own balance is expected to go
    /// arbitrarily negative under the uniform debit-increases/credit-decreases convention
    /// (see GetAccountBalanceAsync) since it only ever gives money away; that's intentional
    /// for a demo-only funding source and is never surfaced to a caller.
    /// </summary>
    public static LedgerAccount OpenSystemFundingAccount(Currency currency, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("A system account must have a display name.", nameof(displayName));
        }

        return new LedgerAccount
        {
            Id = Guid.NewGuid(),
            OwnerUserId = null,
            Type = LedgerAccountType.SystemFunding,
            Currency = currency,
            DisplayName = displayName.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
        };
    }
}
