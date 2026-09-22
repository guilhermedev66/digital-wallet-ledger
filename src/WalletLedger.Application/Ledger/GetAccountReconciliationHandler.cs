using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Dtos;

namespace WalletLedger.Application.Ledger;

public sealed record GetAccountReconciliationQuery(Guid OwnerUserId, Guid WalletId, bool CallerIsAdmin = false);

public sealed class GetAccountReconciliationHandler(IWalletRepository walletRepository, ITransactionRepository transactionRepository)
{
    /// <summary>Same non-leak pattern as GetWalletByIdHandler: null for both "doesn't exist" and "not yours"; an admin caller bypasses ownership.</summary>
    public async Task<ReconciliationReportDto?> HandleAsync(GetAccountReconciliationQuery query, CancellationToken ct)
    {
        var account = await walletRepository.GetByIdAsync(query.WalletId, ct);
        if (account is null || (account.OwnerUserId != query.OwnerUserId && !query.CallerIsAdmin))
        {
            return null;
        }

        var entries = await transactionRepository.ListEntriesForTransactionsTouchingAccountAsync(account.Id, ct);
        var projectedBalance = await transactionRepository.GetAccountBalanceAsync(account.Id, ct);

        var accountReport = ReconciliationEngine.ReconcileAccount(account, projectedBalance, entries);
        var unbalanced = ReconciliationEngine.FindUnbalancedTransactions(entries);

        return new ReconciliationReportDto(DateTime.UtcNow, [accountReport], unbalanced, accountReport.IsBalanced && unbalanced.Count == 0);
    }
}
