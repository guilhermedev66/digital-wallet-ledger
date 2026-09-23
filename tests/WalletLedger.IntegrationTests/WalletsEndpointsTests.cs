using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WalletLedger.Api.Controllers;
using WalletLedger.Application.Dtos;

namespace WalletLedger.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class WalletsEndpointsTests(PostgresContainerFixture postgres)
{
    private HttpClient CreateClient() => new WalletLedgerApiFactory(postgres.ConnectionString).CreateClient();

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string email, string password = "correct horse battery staple")
    {
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password));
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResultDto>();
        return auth!.AccessToken;
    }

    private static void Authorize(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task CreateListAndGet_HappyPath_Succeeds()
    {
        using var client = CreateClient();
        var token = await RegisterAndLoginAsync(client, UniqueEmail());
        Authorize(client, token);

        var createResponse = await client.PostAsJsonAsync("/api/wallets", new CreateWalletRequest("Usd", "My wallet"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<WalletDto>();
        Assert.NotNull(created);

        var listResponse = await client.GetAsync("/api/wallets");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var wallets = await listResponse.Content.ReadFromJsonAsync<List<WalletDto>>();
        Assert.Contains(wallets!, w => w.Id == created!.Id);

        var getResponse = await client.GetAsync($"/api/wallets/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<WalletDto>();
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal("My wallet", fetched.DisplayName);
    }

    [Theory]
    [InlineData("Usd,Brl")]
    [InlineData("Usd,Eur")]
    public async Task Create_CommaCombinedCurrencyValue_ReturnsBadRequest(string currency)
    {
        // M7 security gate finding: Enum.TryParse alone accepts comma-combined values on a
        // non-[Flags] enum by OR-ing their underlying numbers - "Usd,Brl" would otherwise
        // silently parse as Brl (0|1=1) instead of being rejected.
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));

        var response = await client.PostAsJsonAsync("/api/wallets", new CreateWalletRequest(currency, "Should be rejected"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Eur")]
    [InlineData("Gbp")]
    [InlineData("Chf")]
    [InlineData("Cad")]
    [InlineData("Aud")]
    public async Task Create_NewSupportedCurrency_Succeeds(string currency)
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));

        var response = await client.PostAsJsonAsync("/api/wallets", new CreateWalletRequest(currency, "New currency wallet"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<WalletDto>();
        Assert.Equal(currency, created!.Currency);
    }
}
