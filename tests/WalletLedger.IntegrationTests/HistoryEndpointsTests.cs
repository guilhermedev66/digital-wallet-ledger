using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Web;
using WalletLedger.Api.Controllers;
using WalletLedger.Application.Dtos;

namespace WalletLedger.IntegrationTests;

/// <summary>M4's extension of M3's `GET /api/wallets/{id}/history` with date-range and type filters, still page-based (see ARCHITECTURE.md - not switched to cursor-based).</summary>
[Collection(PostgresCollection.Name)]
public sealed class HistoryEndpointsTests(PostgresContainerFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private HttpClient CreateClient() => new WalletLedgerApiFactory(postgres.ConnectionString).CreateClient();

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
        var response = await client.PostAsJsonAsync("/api/wallets", new CreateWalletRequest("Usd", "History test wallet"));
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
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);
    }

    private static async Task<Guid> TransferAsync(HttpClient client, Guid sourceWalletId, Guid destinationWalletId, long amountMinorUnits)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/wallets/{sourceWalletId}/transfer")
        {
            Content = JsonContent.Create(new TransferRequest(destinationWalletId, amountMinorUnits)),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<TransactionDto>();
        return dto!.Id;
    }

    [Fact]
    public async Task GetHistory_FilterByType_OnlyReturnsMatchingTransactions()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);
        var otherWalletId = await CreateWalletAsync(client);

        await FundAsync(client, walletId, 5_000);
        await TransferAsync(client, walletId, otherWalletId, 1_000);

        var response = await client.GetAsync($"/api/wallets/{walletId}/history?type=SimulatedFunding");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<TransactionDto>>();

        Assert.Single(page!.Items);
        Assert.All(page.Items, t => Assert.Equal("SimulatedFunding", t.Type));
    }

    [Fact]
    public async Task GetHistory_FilterByDateRangeExcludingEverything_ReturnsEmptyPage()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);
        await FundAsync(client, walletId, 1_000);

        var farFuture = DateTime.UtcNow.AddYears(10).ToString("O");
        var response = await client.GetAsync($"/api/wallets/{walletId}/history?fromUtc={HttpUtility.UrlEncode(farFuture)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<TransactionDto>>();
        Assert.Empty(page!.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task GetHistory_FromUtcAfterToUtc_ReturnsBadRequest()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);

        var from = HttpUtility.UrlEncode(DateTime.UtcNow.ToString("O"));
        var to = HttpUtility.UrlEncode(DateTime.UtcNow.AddDays(-1).ToString("O"));
        var response = await client.GetAsync($"/api/wallets/{walletId}/history?fromUtc={from}&toUtc={to}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetHistory_UnsupportedType_ReturnsBadRequest()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);

        var response = await client.GetAsync($"/api/wallets/{walletId}/history?type=NotARealType");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetHistory_CommaCombinedTypeValue_ReturnsBadRequest()
    {
        // M7 security gate finding: Enum.TryParse alone accepts comma-combined values on a
        // non-[Flags] enum by OR-ing their underlying numbers - "SimulatedFunding,Reversal"
        // would otherwise silently parse to an undefined value (1|2=3) that matches no row,
        // returning an empty page instead of a 400.
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);

        var response = await client.GetAsync($"/api/wallets/{walletId}/history?type=SimulatedFunding,Reversal");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetHistory_Pagination_SplitsAcrossPages()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);

        for (var i = 0; i < 3; i++)
        {
            await FundAsync(client, walletId, 100);
        }

        var firstPageResponse = await client.GetAsync($"/api/wallets/{walletId}/history?page=1&pageSize=2");
        var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<PagedResult<TransactionDto>>();
        Assert.Equal(2, firstPage!.Items.Count);
        Assert.Equal(3, firstPage.TotalCount);

        var secondPageResponse = await client.GetAsync($"/api/wallets/{walletId}/history?page=2&pageSize=2");
        var secondPage = await secondPageResponse.Content.ReadFromJsonAsync<PagedResult<TransactionDto>>();
        Assert.Single(secondPage!.Items);

        Assert.DoesNotContain(secondPage.Items[0].Id, firstPage.Items.Select(t => t.Id));
    }
}
