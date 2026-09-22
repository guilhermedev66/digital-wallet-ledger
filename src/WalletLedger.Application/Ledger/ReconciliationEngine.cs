using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Dtos;
using WalletLedger.Domain.Entities;

namespace WalletLedger.Application.Ledger;

/// <summary>
/// Pure, DB-free reconciliation logic (see ARCHITECTURE.md's "Reconciliation" section) - takes
/// already-fetched raw LedgerEntryProjection rows so it's trivially unit-testable, including
/// adversarial "deliberately unbalanced" cases that Transaction.Post would refuse to construct.
/// </summary>
internal static class ReconciliationEngine
{
    /// <summary>
    /// account's projected balance is what GetAccountBalanceAsync already returned for it;
    /// entries is any superset of that account's own entries (extra rows for other accounts
    /// are ignored) - recomputed sums only the rows matching account.Id.
    /// </summary>
    public static AccountReconciliationDto ReconcileAccount(LedgerAccount account, long projectedBalance, IReadOnlyList<LedgerEntryProjection> entries)
    {
        var recomputed = entries
            .Where(e => e.AccountId == account.Id)
            .Sum(e => e.Direction == LedgerEntryDirection.Debit ? e.AmountMinorUnits : -e.AmountMinorUnits);

        var drift = projectedBalance - recomputed;

        return new AccountReconciliationDto(
            account.Id, account.Type.ToString(), account.Currency.ToString(),
            projectedBalance, recomputed, drift, drift == 0);
    }

    /// <summary>Re-verifies, straight from raw rows, the same balance invariant Transaction.Post enforces at construction time - a defense-in-depth check against DB tampering or a future bug in a path that bypasses it.</summary>
    public static IReadOnlyList<UnbalancedTransactionDto> FindUnbalancedTransactions(IReadOnlyList<LedgerEntryProjection> entries)
    {
        var result = new List<UnbalancedTransactionDto>();

        foreach (var byTransaction in entries.GroupBy(e => e.TransactionId))
        {
            foreach (var byCurrency in byTransaction.GroupBy(e => e.Currency))
            {
                var debits = byCurrency.Where(e => e.Direction == LedgerEntryDirection.Debit).Sum(e => e.AmountMinorUnits);
                var credits = byCurrency.Where(e => e.Direction == LedgerEntryDirection.Credit).Sum(e => e.AmountMinorUnits);

                if (debits != credits)
                {
                    result.Add(new UnbalancedTransactionDto(byTransaction.Key, byCurrency.Key.ToString(), debits, credits));
                }
            }
        }

        return result;
    }
}
