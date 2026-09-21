using Microsoft.EntityFrameworkCore;
using WalletLedger.Application.Abstractions;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Infrastructure.Persistence;

public sealed class EfUserRepository(WalletLedgerDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByEmailAsync(Email email, CancellationToken ct) =>
        dbContext.Users.SingleOrDefaultAsync(u => u.Email == email, ct);

    public async Task AddAsync(User user, CancellationToken ct)
    {
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(ct);
    }
}
