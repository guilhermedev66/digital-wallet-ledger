using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Domain.Tests;

public class LedgerAccountTests
{
    [Fact]
    public void Open_ThrowsArgumentException_WhenOwnerIdIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => LedgerAccount.Open(Guid.Empty, Currency.Usd));
    }

    [Fact]
    public void Open_SetsExpectedFields_OnSuccess()
    {
        var ownerId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        var account = LedgerAccount.Open(ownerId, Currency.Brl, "My Wallet");

        var after = DateTime.UtcNow;

        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal(ownerId, account.OwnerUserId);
        Assert.Equal(Currency.Brl, account.Currency);
        Assert.Equal("My Wallet", account.DisplayName);
        Assert.InRange(account.CreatedAtUtc, before, after);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Open_NormalizesBlankDisplayName_ToNull(string? displayName)
    {
        var account = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd, displayName);

        Assert.Null(account.DisplayName);
    }

    [Fact]
    public void Open_TrimsNonBlankDisplayName()
    {
        var account = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd, "  Savings  ");

        Assert.Equal("Savings", account.DisplayName);
    }

    [Fact]
    public void Open_DefaultsDisplayNameToNull_WhenOmitted()
    {
        var account = LedgerAccount.Open(Guid.NewGuid(), Currency.Usd);

        Assert.Null(account.DisplayName);
    }
}
