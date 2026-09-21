using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WalletLedger.Api.Controllers;
using WalletLedger.Application.Dtos;

namespace WalletLedger.IntegrationTests;

/// <summary>
/// The actual proof for M3's fund-safety claims: concurrent transfers can't double-spend
/// past a wallet's real balance, and idempotency replay is safe under a real race, not just
/// in the happy path. See EfTransactionRepository.PostTransferIfSufficientFundsAsync for the
/// row-locking mechanism these tests are trying to break.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class TransferConcurrencyTests(PostgresContainerFixture postgres)
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
        var response = await client.PostAsJsonAsync("/api/wallets", new CreateWalletRequest("Usd", "Transfer test wallet"));
        var wallet = await response.Content.ReadFromJsonAsync<WalletDto>();
        return wallet!.Id;
    }

    private static async Task<HttpResponseMessage> SimulateFundingAsync(HttpClient client, Guid walletId, long amountMinorUnits, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/wallets/{walletId}/simulate-funding")
        {
            Content = JsonContent.Create(new SimulateFundingRequest(amountMinorUnits)),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    private static async Task FundAsync(HttpClient client, Guid walletId, long amountMinorUnits)
    {
        var response = await SimulateFundingAsync(client, walletId, amountMinorUnits, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> TransferAsync(
        HttpClient client, Guid sourceWalletId, Guid destinationWalletId, long amountMinorUnits, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/wallets/{sourceWalletId}/transfer")
        {
            Content = JsonContent.Create(new TransferRequest(destinationWalletId, amountMinorUnits)),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    private static async Task<long> GetBalanceAsync(HttpClient client, Guid walletId)
    {
        var response = await client.GetAsync($"/api/wallets/{walletId}/balance");
        var balance = await response.Content.ReadFromJsonAsync<WalletBalanceDto>();
        return balance!.BalanceMinorUnits;
    }

    [Fact]
    public async Task ConcurrentTransfers_DrainingPastBalance_OnlyOneSucceeds()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));

        var source = await CreateWalletAsync(client);
        var destA = await CreateWalletAsync(client);
        var destB = await CreateWalletAsync(client);
        await FundAsync(client, source, 1_000);

        var results = await Task.WhenAll(
            TransferAsync(client, source, destA, 600, Guid.NewGuid().ToString()),
            TransferAsync(client, source, destB, 600, Guid.NewGuid().ToString()));

        var statusCodes = results.Select(r => r.StatusCode).ToList();

        // Never both succeed (double-spend), never both fail (a wrong/over-broad lock could
        // reject both), never a 500 (an unhandled exception from the race itself).
        Assert.DoesNotContain(HttpStatusCode.InternalServerError, statusCodes);
        Assert.Equal(1, statusCodes.Count(c => c == HttpStatusCode.OK));
        Assert.Equal(1, statusCodes.Count(c => c == HttpStatusCode.UnprocessableEntity));

        // Exactly the successful transfer's debit landed - not 1000 (neither posted) and not
        // -200 (both posted, the actual double-spend this mechanism exists to prevent).
        Assert.Equal(400, await GetBalanceAsync(client, source));

        var aSucceeded = results[0].StatusCode == HttpStatusCode.OK;
        Assert.Equal(aSucceeded ? 600 : 0, await GetBalanceAsync(client, destA));
        Assert.Equal(aSucceeded ? 0 : 600, await GetBalanceAsync(client, destB));
    }

    [Fact]
    public async Task ConcurrentIdenticalReplay_BothSucceed_SameTransactionId_DebitedOnce()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));

        var source = await CreateWalletAsync(client);
        var destination = await CreateWalletAsync(client);
        await FundAsync(client, source, 1_000);

        var key = Guid.NewGuid().ToString();

        // Correctness here doesn't depend on the two requests truly overlapping at the DB
        // level - Task.WhenAll only guarantees both are in flight, not perfect simultaneity.
        // Either ordering must produce the same outcome: if the first commits before the
        // second's pre-check runs, the second finds it via FindByIdempotencyKeyAsync and never
        // touches the DB constraint; if both pre-checks run before either insert, one wins the
        // insert and the other hits the unique-constraint race path. Both paths must return the
        // same transaction id and post exactly one debit - that's what's actually asserted.
        var results = await Task.WhenAll(
            TransferAsync(client, source, destination, 200, key),
            TransferAsync(client, source, destination, 200, key));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        var tx1 = await results[0].Content.ReadFromJsonAsync<TransactionDto>();
        var tx2 = await results[1].Content.ReadFromJsonAsync<TransactionDto>();
        Assert.Equal(tx1!.Id, tx2!.Id);

        Assert.Equal(800, await GetBalanceAsync(client, source));
        Assert.Equal(200, await GetBalanceAsync(client, destination));
    }

    [Fact]
    public async Task SequentialReplay_SameKeySameParameters_ReturnsOriginal_DebitedOnce()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));

        var source = await CreateWalletAsync(client);
        var destination = await CreateWalletAsync(client);
        await FundAsync(client, source, 1_000);

        var key = Guid.NewGuid().ToString();

        var first = await TransferAsync(client, source, destination, 250, key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstTx = await first.Content.ReadFromJsonAsync<TransactionDto>();

        var second = await TransferAsync(client, source, destination, 250, key);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondTx = await second.Content.ReadFromJsonAsync<TransactionDto>();

        Assert.Equal(firstTx!.Id, secondTx!.Id);
        Assert.Equal(750, await GetBalanceAsync(client, source));
    }

    [Fact]
    public async Task SequentialReplay_SameKeyDifferentParameters_ReturnsConflict_NoSideEffect()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));

        var source = await CreateWalletAsync(client);
        var destinationA = await CreateWalletAsync(client);
        var destinationB = await CreateWalletAsync(client);
        await FundAsync(client, source, 1_000);

        var key = Guid.NewGuid().ToString();

        var original = await TransferAsync(client, source, destinationA, 300, key);
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);

        // Same key, same destination, different amount.
        var differentAmount = await TransferAsync(client, source, destinationA, 500, key);
        Assert.Equal(HttpStatusCode.Conflict, differentAmount.StatusCode);

        // Same key, same amount, different destination.
        var differentDestination = await TransferAsync(client, source, destinationB, 300, key);
        Assert.Equal(HttpStatusCode.Conflict, differentDestination.StatusCode);

        // Neither conflicting replay had any effect - only the original 300 debit landed.
        Assert.Equal(700, await GetBalanceAsync(client, source));
        Assert.Equal(300, await GetBalanceAsync(client, destinationA));
        Assert.Equal(0, await GetBalanceAsync(client, destinationB));
    }

    [Fact]
    public async Task Replay_AfterOriginalFailedWithInsufficientFunds_SucceedsOnceFundsExist()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));

        var source = await CreateWalletAsync(client);
        var destination = await CreateWalletAsync(client);
        await FundAsync(client, source, 100);

        var key = Guid.NewGuid().ToString();

        var failedAttempt = await TransferAsync(client, source, destination, 500, key);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, failedAttempt.StatusCode);

        // The failed attempt never persisted a Transaction row under `key` (Post is only
        // called, and only inserted, after the funds check passes) - fund further, then retry
        // the exact same request. It must be treated as a fresh attempt, not blocked by the
        // earlier failure under the same key.
        await FundAsync(client, source, 500);

        var retry = await TransferAsync(client, source, destination, 500, key);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);

        Assert.Equal(100, await GetBalanceAsync(client, source));
        Assert.Equal(500, await GetBalanceAsync(client, destination));
    }

    [Fact]
    public async Task ConcurrentTransfers_FromUnrelatedSources_BothSucceed()
    {
        // Guards against an overly-broad lock (e.g. a whole-table lock instead of one row)
        // that would serialize even unrelated transfers - not asserting on timing, just that
        // both independently succeed.
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));

        var sourceA = await CreateWalletAsync(client);
        var sourceB = await CreateWalletAsync(client);
        var destination = await CreateWalletAsync(client);
        await FundAsync(client, sourceA, 1_000);
        await FundAsync(client, sourceB, 1_000);

        var results = await Task.WhenAll(
            TransferAsync(client, sourceA, destination, 300, Guid.NewGuid().ToString()),
            TransferAsync(client, sourceB, destination, 400, Guid.NewGuid().ToString()));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(700, await GetBalanceAsync(client, sourceA));
        Assert.Equal(600, await GetBalanceAsync(client, sourceB));
        Assert.Equal(700, await GetBalanceAsync(client, destination));
    }
}
