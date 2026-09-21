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
        // One SQL statement (a single SUM over a signed CASE expression), not two separate
        // debit/credit sums - two round trips would leave a torn-read window where a
        // concurrent post between them could be reflected in one sum but not the other.
        // M3's insufficient-funds check is going to gate a withdrawal decision on this
        // exact result, so that gap would be a real financial-correctness bug, not just a
        // display glitch.
        return await dbContext.LedgerEntries
            .Where(e => e.AccountId == accountId)
            .SumAsync(e => (long?)(e.Direction == LedgerEntryDirection.Debit ? e.AmountMinorUnits : -e.AmountMinorUnits), ct)
            ?? 0;
    }
}
