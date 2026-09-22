namespace WalletLedger.Application.Dtos;

/// <summary>
/// ProjectedBalanceMinorUnits is the balance every other endpoint treats as authoritative
/// (ITransactionRepository.GetAccountBalanceAsync's SQL SUM(CASE...) query).
/// RecomputedBalanceMinorUnits is the same account's balance re-derived independently, by
/// summing its raw LedgerEntry rows in application code instead (see ReconciliationEngine) -
/// a structurally different code path, not the same computation run twice. DriftMinorUnits is
/// their difference; it's always 0 today (there is no separate cached/materialized balance
/// projection in this codebase yet - see ARCHITECTURE.md's "Balances" section), but the shape
/// exists so introducing one later doesn't require an API change, and this still catches a
/// real divergence between the two code paths themselves.
/// </summary>
public sealed record AccountReconciliationDto(
    Guid AccountId,
    string AccountType,
    string Currency,
    long ProjectedBalanceMinorUnits,
    long RecomputedBalanceMinorUnits,
    long DriftMinorUnits,
    bool IsBalanced);

/// <summary>A transaction whose entries, grouped by currency, don't have equal debit and credit totals - structurally impossible via Transaction.Post, so finding one here means DB tampering or a bug bypassing that path.</summary>
public sealed record UnbalancedTransactionDto(Guid TransactionId, string Currency, long TotalDebitMinorUnits, long TotalCreditMinorUnits);

public sealed record ReconciliationReportDto(
    DateTime GeneratedAtUtc,
    IReadOnlyList<AccountReconciliationDto> Accounts,
    IReadOnlyList<UnbalancedTransactionDto> UnbalancedTransactions,
    bool IsClean);
