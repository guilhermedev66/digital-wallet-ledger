using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Dtos;

namespace WalletLedger.Application.Ledger;

public sealed record GetGlobalReconciliationQuery(bool CallerIsAdmin);

/// <summary>
/// Reconciles every LedgerAccount in the system, not just the caller's own - admin-only (see
/// ReconciliationController). Non-admin callers get null (404), the same non-leak pattern used
/// everywhere else in this API rather than a 403 (see MEMORY.md's "never a 403" convention) -
/// this isn't ownership-scoped, but the same reasoning applies: don't confirm to a non-admin
/// caller that this admin-only capability even exists.
/// </summary>
public sealed class GetGlobalReconciliationHandler(IWalletRepository walletRepository, ITransactionRepository transactionRepository)
{
    public async Task<ReconciliationReportDto?> HandleAsync(GetGlobalReconciliationQuery query, CancellationToken ct)
    {
        if (!query.CallerIsAdmin)
        {
            return null;
        }

        var accounts = await walletRepository.ListAllAsync(ct);
        var entries = await transactionRepository.ListAllEntriesAsync(ct);

        var accountReports = new List<AccountReconciliationDto>();
        foreach (var account in accounts)
        {
            var projectedBalance = await transactionRepository.GetAccountBalanceAsync(account.Id, ct);
            accountReports.Add(ReconciliationEngine.ReconcileAccount(account, projectedBalance, entries));
        }

        var unbalanced = ReconciliationEngine.FindUnbalancedTransactions(entries);

        return new ReconciliationReportDto(
            DateTime.UtcNow,
            accountReports,
            unbalanced,
            accountReports.All(a => a.IsBalanced) && unbalanced.Count == 0);
    }
}
