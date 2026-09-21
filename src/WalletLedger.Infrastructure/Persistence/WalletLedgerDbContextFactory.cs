using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WalletLedger.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so `dotnet ef migrations` works without full app DI. The fallback
/// connection string is the same throwaway local-dev value already committed in
/// .env.example — never a real secret.
/// </summary>
public sealed class WalletLedgerDbContextFactory : IDesignTimeDbContextFactory<WalletLedgerDbContext>
{
    public WalletLedgerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__DEFAULT")
            ?? "Host=localhost;Port=5432;Database=walletledger;Username=walletledger;Password=walletledger_dev_only";

        var optionsBuilder = new DbContextOptionsBuilder<WalletLedgerDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new WalletLedgerDbContext(optionsBuilder.Options);
    }
}
