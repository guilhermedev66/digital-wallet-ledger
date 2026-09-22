using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WalletLedger.Api.Controllers;
using WalletLedger.Application.Dtos;
using WalletLedger.Domain.ValueObjects;
using WalletLedger.Infrastructure.Persistence;

namespace WalletLedger.IntegrationTests;

/// <summary>The real proof for M4's reconciliation claims: against a real PostgreSQL, a clean ledger reports IsClean, and account-level ownership/admin gating matches the rest of this API (see ARCHITECTURE.md's "Reconciliation" section).</summary>
[Collection(PostgresCollection.Name)]
public sealed class ReconciliationEndpointsTests(PostgresContainerFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private HttpClient CreateClient(WalletLedgerApiFactory factory) => factory.CreateClient();

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string email)
    {
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, Password));
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResultDto>();
        return auth!.AccessToken;
    }

    private static void Authorize(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task<Guid> CreateWalletAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/wallets", new CreateWalletRequest("Usd", "Reconciliation test wallet"));
        var wallet = await response.Content.ReadFromJsonAsync<WalletDto>();
        return wallet!.Id;
    }

    private static async Task FundAsync(HttpClient client, Guid walletId, long amountMinorUnits)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/wallets/{walletId}/simulate-funding")
        {
            Content = JsonContent.Create(new SimulateFundingRequest(amountMinorUnits)),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAccountReconciliation_AfterFundingAndTransfer_IsClean()
    {
        using var factory = new WalletLedgerApiFactory(postgres.ConnectionString);

        using var sourceClient = CreateClient(factory);
        Authorize(sourceClient, await RegisterAndLoginAsync(sourceClient, UniqueEmail()));
        var sourceWalletId = await CreateWalletAsync(sourceClient);
        await FundAsync(sourceClient, sourceWalletId, 10_000);

        using var destinationClient = CreateClient(factory);
        Authorize(destinationClient, await RegisterAndLoginAsync(destinationClient, UniqueEmail()));
        var destinationWalletId = await CreateWalletAsync(destinationClient);

        var transferRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/wallets/{sourceWalletId}/transfer")
        {
            Content = JsonContent.Create(new TransferRequest(destinationWalletId, 4_000)),
        };
        transferRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, (await sourceClient.SendAsync(transferRequest)).StatusCode);

        var response = await sourceClient.GetAsync($"/api/wallets/{sourceWalletId}/reconciliation");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<ReconciliationReportDto>();

        Assert.True(report!.IsClean);
        Assert.Empty(report.UnbalancedTransactions);
        var accountReport = Assert.Single(report.Accounts);
        Assert.Equal(sourceWalletId, accountReport.AccountId);
        Assert.Equal(6_000, accountReport.ProjectedBalanceMinorUnits);
        Assert.Equal(6_000, accountReport.RecomputedBalanceMinorUnits);
        Assert.Equal(0, accountReport.DriftMinorUnits);
        Assert.True(accountReport.IsBalanced);
    }

    [Fact]
    public async Task GetAccountReconciliation_WalletBelongsToDifferentUser_ReturnsNotFound_NeverForbidden()
    {
        using var factory = new WalletLedgerApiFactory(postgres.ConnectionString);

        using var client = CreateClient(factory);
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);

        using var otherClient = CreateClient(factory);
        Authorize(otherClient, await RegisterAndLoginAsync(otherClient, UniqueEmail()));

        var response = await otherClient.GetAsync($"/api/wallets/{walletId}/reconciliation");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetGlobalReconciliation_NonAdmin_ReturnsNotFound_NeverForbidden()
    {
        using var factory = new WalletLedgerApiFactory(postgres.ConnectionString);

        using var client = CreateClient(factory);
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        await CreateWalletAsync(client);

        var response = await client.GetAsync("/api/reconciliation");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetGlobalReconciliation_Admin_ReturnsCleanReportAcrossAccounts()
    {
        using var factory = new WalletLedgerApiFactory(postgres.ConnectionString);

        using var userClient = CreateClient(factory);
        Authorize(userClient, await RegisterAndLoginAsync(userClient, UniqueEmail()));
        var walletId = await CreateWalletAsync(userClient);
        await FundAsync(userClient, walletId, 3_000);

        var adminEmail = UniqueEmail();
        using var adminClient = CreateClient(factory);
        await RegisterAndLoginAsync(adminClient, adminEmail);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<WalletLedgerDbContext>();
            var adminUser = await dbContext.Users.SingleAsync(u => u.Email == Email.Parse(adminEmail));
            adminUser.PromoteToAdmin();
            await dbContext.SaveChangesAsync();
        }

        // Re-login: the JWT only carries "role":"admin" for whatever IsAdmin was at login time.
        Authorize(adminClient, await RegisterAndLoginAsync(adminClient, adminEmail));

        var response = await adminClient.GetAsync("/api/reconciliation");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<ReconciliationReportDto>();

        Assert.True(report!.IsClean);
        Assert.Empty(report.UnbalancedTransactions);
        // At least the funded wallet, the admin's own (empty) wallet-less account set, and the SystemFunding account.
        Assert.Contains(report.Accounts, a => a.AccountId == walletId && a.ProjectedBalanceMinorUnits == 3_000);
        Assert.All(report.Accounts, a => Assert.True(a.IsBalanced));
    }
}
