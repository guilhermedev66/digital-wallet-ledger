using Microsoft.EntityFrameworkCore;
using WalletLedger.Domain.Entities;

namespace WalletLedger.Infrastructure.Persistence;

public sealed class WalletLedgerDbContext(DbContextOptions<WalletLedgerDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WalletLedgerDbContext).Assembly);
    }
}
