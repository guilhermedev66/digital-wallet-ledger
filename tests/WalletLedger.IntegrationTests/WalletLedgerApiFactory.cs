using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WalletLedger.Infrastructure.Persistence;

namespace WalletLedger.IntegrationTests;

public sealed class WalletLedgerApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<WalletLedgerDbContext>>();

            services.AddDbContext<WalletLedgerDbContext>(options => options.UseNpgsql(connectionString));

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<WalletLedgerDbContext>();
            dbContext.Database.Migrate();
        });
    }
}
