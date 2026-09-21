using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Domain.Tests;

/// <summary>
/// Adversarial coverage for Transaction.Post - the one place the "a transaction always
/// balances" invariant is enforced (see ARCHITECTURE.md, "Invariant: a transaction
/// balances"). Every test here is an attempt to construct something that shouldn't exist;
/// each must throw, never silently accept or "fix up" bad input.
/// </summary>
public class TransactionTests
{
    private static readonly Guid RequesterId = Guid.NewGuid();
    private static readonly Guid AccountA = Guid.NewGuid();
    private static readonly Guid AccountB = Guid.NewGuid();
    private const string IdempotencyKey = "idem-key-1";

    private static LedgerEntryLine Debit(Guid accountId, long amount, Currency currency = Currency.Usd) =>
        new(accountId, LedgerEntryDirection.Debit, amount, currency);

    private static LedgerEntryLine Credit(Guid accountId, long amount, Currency currency = Currency.Usd) =>
        new(accountId, LedgerEntryDirection.Credit, amount, currency);

    // --- Happy path -----------------------------------------------------------------

    [Fact]
    public void Post_BalancedTwoEntryTransaction_Succeeds()
    {
        var transaction = Transaction.Post(
            RequesterId, IdempotencyKey, TransactionType.SimulatedFunding,
            [Debit(AccountA, 1_000), Credit(AccountB, 1_000)]);

        Assert.NotEqual(Guid.Empty, transaction.Id);
        Assert.Equal(TransactionType.SimulatedFunding, transaction.Type);
        Assert.Equal(2, transaction.Entries.Count);
        Assert.All(transaction.Entries, e => Assert.Equal(transaction.Id, e.TransactionId));
        Assert.Contains(transaction.Entries, e => e.AccountId == AccountA && e.Direction == LedgerEntryDirection.Debit && e.AmountMinorUnits == 1_000);
        Assert.Contains(transaction.Entries, e => e.AccountId == AccountB && e.Direction == LedgerEntryDirection.Credit && e.AmountMinorUnits == 1_000);
    }

    [Fact]
    public void Post_BalancedMultiEntryTransaction_Succeeds()
    {
        // Split debit across two accounts, single credit covering the total - still balances.
        var transaction = Transaction.Post(
            RequesterId, IdempotencyKey, TransactionType.Transfer,
            [Debit(AccountA, 300), Debit(AccountA, 700), Credit(AccountB, 1_000)]);

        Assert.Equal(3, transaction.Entries.Count);
    }

    // --- Attempts to break the balance invariant -------------------------------------

    [Fact]
    public void Post_DebitsExceedCredits_Throws()
    {
        Assert.Throws<ArgumentException>(() => Transaction.Post(
            RequesterId, IdempotencyKey, TransactionType.Transfer,
            [Debit(AccountA, 1_001), Credit(AccountB, 1_000)]));
    }

    [Fact]
    public void Post_CreditsExceedDebits_Throws()
    {
        Assert.Throws<ArgumentException>(() => Transaction.Post(
            RequesterId, IdempotencyKey, TransactionType.Transfer,
            [Debit(AccountA, 999), Credit(AccountB, 1_000)]));
    }

    [Fact]
    public void Post_OffByOneMinorUnit_Throws()
    {
        // The classic rounding-bug shape: looks balanced to the eye, isn't to the cent.
        Assert.Throws<ArgumentException>(() => Transaction.Post(
            RequesterId, IdempotencyKey, TransactionType.Transfer,
            [Debit(AccountA, 100_000), Credit(AccountB, 99_999)]));
    }

    [Fact]
    public void Post_AllDebitsNoCredits_Throws()
    {
        // Individually "balanced" (there's nothing to be unequal to) but not a real
        // double-entry transaction - must still be rejected.
        Assert.Throws<ArgumentException>(() => Transaction.Post(
            RequesterId, IdempotencyKey, TransactionType.Transfer,
            [Debit(AccountA, 500), Debit(AccountB, 500)]));
    }

    [Fact]
    public void Post_AllCreditsNoDebits_Throws()
    {
        Assert.Throws<ArgumentException>(() => Transaction.Post(
            RequesterId, IdempotencyKey, TransactionType.Transfer,
            [Credit(AccountA, 500), Credit(AccountB, 500)]));
    }

    [Fact]
    public void Post_NoEntries_Throws()
    {
        Assert.Throws<ArgumentException>(() => Transaction.Post(
            RequesterId, IdempotencyKey, TransactionType.Transfer, []));
    }

    [Fact]
    public void Post_NullEntries_Throws()
    {
        Assert.Throws<ArgumentException>(() => Transaction.Post(
            RequesterId, IdempotencyKey, TransactionType.Transfer, null!));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(-1_000_000L)]
    public void Post_NonPositiveEntryAmount_Throws(long amount)
    {
        // A zero or negative "debit" is exactly how you'd smuggle a phantom balance change
        // past a naive sum-based check - reject it outright regardless of what it's paired with.
        Assert.Throws<ArgumentException>(() => Transaction.Post(
            RequesterId, IdempotencyKey, TransactionType.Transfer,
            [Debit(AccountA, amount), Credit(AccountB, Math.Max(amount, 1))]));
    }

    [Fact]
    public void Post_NegativeDebitOffsettingInflatedCredit_StillRejected()
    {
        // Attempt: a negative debit plus an oversized credit that "sums to balance" under
        // naive debit-total == credit-total arithmetic (-500 + 1500 credit... doesn't even
        // reach here since totals wouldn't match, but the point is the negative amount alone
        // must be rejected before any sum comparison could be tricked).
        Assert.Throws<ArgumentException>(() => Transaction.Post(
            RequesterId, IdempotencyKey, TransactionType.Transfer,
            [Debit(AccountA, -500), Credit(AccountB, 500)]));
    }

    [Fact]
    public void Post_MixedCurrencies_Throws()
    {
        // Debits and credits individually equal (1000 == 1000) but in different currencies -
        // must not be treated as balanced. Also exercises the "one currency per transaction" rule.
        Assert.Throws<ArgumentException>(() => Transaction.Post(
            RequesterId, IdempotencyKey, TransactionType.Transfer,
            [Debit(AccountA, 1_000, Currency.Usd), Credit(AccountB, 1_000, Currency.Brl)]));
    }

    [Fact]
    public void Post_EmptyRequesterId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Transaction.Post(
            Guid.Empty, IdempotencyKey, TransactionType.Transfer,
            [Debit(AccountA, 1_000), Credit(AccountB, 1_000)]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Post_MissingIdempotencyKey_Throws(string? key)
    {
        Assert.Throws<ArgumentException>(() => Transaction.Post(
            RequesterId, key!, TransactionType.Transfer,
            [Debit(AccountA, 1_000), Credit(AccountB, 1_000)]));
    }

    [Fact]
    public void Post_ReversalWithoutReferencingOriginal_Throws()
    {
        Assert.Throws<ArgumentException>(() => Transaction.Post(
            RequesterId, IdempotencyKey, TransactionType.Reversal,
            [Debit(AccountA, 1_000), Credit(AccountB, 1_000)]));
    }

    [Fact]
    public void Post_NonReversalReferencingAnotherTransaction_Throws()
    {
        Assert.Throws<ArgumentException>(() => Transaction.Post(
            RequesterId, IdempotencyKey, TransactionType.Transfer,
            [Debit(AccountA, 1_000), Credit(AccountB, 1_000)],
            reversalOfTransactionId: Guid.NewGuid()));
    }

    [Fact]
    public void Post_ReversalReferencingOriginal_Succeeds()
    {
        var originalId = Guid.NewGuid();

        var reversal = Transaction.Post(
            RequesterId, IdempotencyKey, TransactionType.Reversal,
            [Debit(AccountB, 1_000), Credit(AccountA, 1_000)],
            reversalOfTransactionId: originalId);

        Assert.Equal(originalId, reversal.ReversalOfTransactionId);
    }

    [Fact]
    public void Entries_CannotBeMutatedFromOutside()
    {
        // Entries is IReadOnlyList<T> with no exposed Add/Remove - the only compile-time-
        // enforced guarantee. This test documents that expectation rather than "prove" it
        // (a missing mutator can't be exercised by a passing test), but if this ever starts
        // compiling because someone widened the return type, that's the regression to catch.
        var transaction = Transaction.Post(
            RequesterId, IdempotencyKey, TransactionType.SimulatedFunding,
            [Debit(AccountA, 1_000), Credit(AccountB, 1_000)]);

        Assert.IsAssignableFrom<IReadOnlyList<LedgerEntry>>(transaction.Entries);
    }
}
