using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WalletLedger.Api.Controllers;
using WalletLedger.Application.Dtos;

namespace WalletLedger.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class SimulatedFundingEndpointsTests(PostgresContainerFixture postgres)
{
    private const long MaxAmountMinorUnits = 1_000_000_00;

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

    private static async Task<HttpResponseMessage> SimulateFundingAsync(
        HttpClient client, Guid walletId, long amountMinorUnits, string? idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/wallets/{walletId}/simulate-funding")
        {
            Content = JsonContent.Create(new SimulateFundingRequest(amountMinorUnits)),
        };

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return await client.SendAsync(request);
    }

    private static async Task<Guid> CreateWalletAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/wallets", new CreateWalletRequest("Usd", "Funding test wallet"));
        var wallet = await response.Content.ReadFromJsonAsync<WalletDto>();
        return wallet!.Id;
    }

    [Fact]
    public async Task SimulateFunding_ThenGetBalance_ReflectsFundedAmount()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);

        var fundResponse = await SimulateFundingAsync(client, walletId, 5_000, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, fundResponse.StatusCode);
        var transaction = await fundResponse.Content.ReadFromJsonAsync<TransactionDto>();
        Assert.NotNull(transaction);
        Assert.Equal("SimulatedFunding", transaction!.Type);
        Assert.Equal(2, transaction.Entries.Count);

        var balanceResponse = await client.GetAsync($"/api/wallets/{walletId}/balance");
        Assert.Equal(HttpStatusCode.OK, balanceResponse.StatusCode);
        var balance = await balanceResponse.Content.ReadFromJsonAsync<WalletBalanceDto>();
        Assert.Equal(5_000, balance!.BalanceMinorUnits);
    }

    [Fact]
    public async Task SimulateFunding_TwiceWithDifferentKeys_BalanceIsSumOfBothEntries()
    {
        // The real proof that "balance == sum of entries" holds through the actual HTTP+DB
        // path (unit tests mock the repository and can't show this) - entries must accumulate,
        // never overwrite.
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);

        var first = await SimulateFundingAsync(client, walletId, 1_500, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await SimulateFundingAsync(client, walletId, 2_500, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var balanceResponse = await client.GetAsync($"/api/wallets/{walletId}/balance");
        var balance = await balanceResponse.Content.ReadFromJsonAsync<WalletBalanceDto>();
        Assert.Equal(4_000, balance!.BalanceMinorUnits);
    }

    [Fact]
    public async Task SimulateFunding_MissingIdempotencyKeyHeader_ReturnsBadRequest()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);

        var response = await SimulateFundingAsync(client, walletId, 1_000, idempotencyKey: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(MaxAmountMinorUnits + 1)]
    public async Task SimulateFunding_AmountOutOfRange_ReturnsBadRequest(long amount)
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);

        var response = await SimulateFundingAsync(client, walletId, amount, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SimulateFunding_WalletBelongsToDifferentUser_ReturnsNotFound_NeverForbidden()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);

        using var otherClient = CreateClient();
        Authorize(otherClient, await RegisterAndLoginAsync(otherClient, UniqueEmail()));

        var response = await SimulateFundingAsync(otherClient, walletId, 1_000, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBalance_NoAuthorizationHeader_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);

        using var anonymousClient = CreateClient();
        var response = await anonymousClient.GetAsync($"/api/wallets/{walletId}/balance");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBalance_FreshlyCreatedWallet_ReturnsZero()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);

        var response = await client.GetAsync($"/api/wallets/{walletId}/balance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var balance = await response.Content.ReadFromJsonAsync<WalletBalanceDto>();
        Assert.Equal(0, balance!.BalanceMinorUnits);
    }
}
