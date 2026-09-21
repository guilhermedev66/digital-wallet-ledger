using Microsoft.EntityFrameworkCore;
using WalletLedger.Application.Abstractions;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Infrastructure.Persistence;

public sealed class EfWalletRepository(WalletLedgerDbContext dbContext) : IWalletRepository
{
    public async Task AddAsync(LedgerAccount account, CancellationToken ct)
    {
        dbContext.LedgerAccounts.Add(account);
        await dbContext.SaveChangesAsync(ct);
    }

    public Task<LedgerAccount?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.LedgerAccounts.SingleOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<LedgerAccount>> ListByOwnerAsync(Guid ownerUserId, CancellationToken ct) =>
        await dbContext.LedgerAccounts
            .Where(a => a.OwnerUserId == ownerUserId)
            .OrderBy(a => a.CreatedAtUtc)
            .ToListAsync(ct);

    public Task<LedgerAccount?> GetSystemFundingAccountAsync(Currency currency, CancellationToken ct) =>
        dbContext.LedgerAccounts.SingleOrDefaultAsync(
            a => a.Type == LedgerAccountType.SystemFunding && a.Currency == currency, ct);
}
