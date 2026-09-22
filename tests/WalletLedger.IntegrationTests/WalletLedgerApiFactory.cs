using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WalletLedger.Infrastructure.Persistence;

namespace WalletLedger.IntegrationTests;

public sealed class WalletLedgerApiFactory(string connectionString, Action<IServiceCollection>? configureTestServices = null) : WebApplicationFactory<Program>
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

            // Applied last so a test-provided override (e.g. a throwing fake, for the global
            // exception handler regression test) wins over the app's own registrations above.
            configureTestServices?.Invoke(services);
        });
    }
}
