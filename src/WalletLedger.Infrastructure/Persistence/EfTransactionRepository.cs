using Microsoft.EntityFrameworkCore;
using WalletLedger.Application.Abstractions;
using WalletLedger.Domain.Entities;

namespace WalletLedger.Infrastructure.Persistence;

public sealed class EfTransactionRepository(WalletLedgerDbContext dbContext) : ITransactionRepository
{
    public async Task AddAsync(Transaction transaction, CancellationToken ct)
    {
        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<long> GetAccountBalanceAsync(Guid accountId, CancellationToken ct)
    {
        var debits = await dbContext.LedgerEntries
            .Where(e => e.AccountId == accountId && e.Direction == LedgerEntryDirection.Debit)
            .SumAsync(e => (long?)e.AmountMinorUnits, ct) ?? 0;

        var credits = await dbContext.LedgerEntries
            .Where(e => e.AccountId == accountId && e.Direction == LedgerEntryDirection.Credit)
            .SumAsync(e => (long?)e.AmountMinorUnits, ct) ?? 0;

        return debits - credits;
    }
}
