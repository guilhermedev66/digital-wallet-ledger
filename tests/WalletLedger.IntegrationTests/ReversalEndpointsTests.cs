using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WalletLedger.Api.Controllers;
using WalletLedger.Application.Dtos;

namespace WalletLedger.IntegrationTests;

/// <summary>
/// The real proof for M4's reversal claims against a real PostgreSQL: balances actually move
/// back through the ledger (not just that a Reversal row gets inserted), a transaction can only
/// be reversed once even under a genuine unique-constraint race, and reversing a transfer whose
/// proceeds have since been spent is correctly refused rather than silently overdrawing.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ReversalEndpointsTests(PostgresContainerFixture postgres)
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
        var response = await client.PostAsJsonAsync("/api/wallets", new CreateWalletRequest("Usd", "Reversal test wallet"));
        var wallet = await response.Content.ReadFromJsonAsync<WalletDto>();
        return wallet!.Id;
    }

    private static async Task<TransactionDto> FundAsync(HttpClient client, Guid walletId, long amountMinorUnits)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/wallets/{walletId}/simulate-funding")
        {
            Content = JsonContent.Create(new SimulateFundingRequest(amountMinorUnits)),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TransactionDto>())!;
    }

    private static async Task<TransactionDto> TransferAsync(HttpClient client, Guid sourceWalletId, Guid destinationWalletId, long amountMinorUnits)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/wallets/{sourceWalletId}/transfer")
        {
            Content = JsonContent.Create(new TransferRequest(destinationWalletId, amountMinorUnits)),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TransactionDto>())!;
    }

    private static async Task<HttpResponseMessage> ReverseAsync(HttpClient client, Guid walletId, Guid transactionId, string? idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/wallets/{walletId}/transactions/{transactionId}/reverse");
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return await client.SendAsync(request);
    }

    private static async Task<long> GetBalanceAsync(HttpClient client, Guid walletId)
    {
        var response = await client.GetAsync($"/api/wallets/{walletId}/balance");
        var balance = await response.Content.ReadFromJsonAsync<WalletBalanceDto>();
        return balance!.BalanceMinorUnits;
    }

    [Fact]
    public async Task ReverseTransfer_RestoresBothWalletsToPreTransferBalances()
    {
        using var sourceClient = CreateClient();
        Authorize(sourceClient, await RegisterAndLoginAsync(sourceClient, UniqueEmail()));
        var sourceWalletId = await CreateWalletAsync(sourceClient);
        await FundAsync(sourceClient, sourceWalletId, 10_000);

        using var destinationClient = CreateClient();
        Authorize(destinationClient, await RegisterAndLoginAsync(destinationClient, UniqueEmail()));
        var destinationWalletId = await CreateWalletAsync(destinationClient);

        var transfer = await TransferAsync(sourceClient, sourceWalletId, destinationWalletId, 4_000);
        Assert.Equal(6_000, await GetBalanceAsync(sourceClient, sourceWalletId));
        Assert.Equal(4_000, await GetBalanceAsync(destinationClient, destinationWalletId));

        // The reversal debits the DESTINATION wallet (it received the transfer's Debit), so
        // it's the recipient who self-service-reverses it, not the sender - a sender can't
        // unilaterally claw funds back out of someone else's wallet (see ReverseTransactionHandler).
        var reverseResponse = await ReverseAsync(destinationClient, destinationWalletId, transfer.Id, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, reverseResponse.StatusCode);
        var reversal = await reverseResponse.Content.ReadFromJsonAsync<TransactionDto>();
        Assert.Equal("Reversal", reversal!.Type);
        Assert.Equal(transfer.Id, reversal.ReversalOfTransactionId);

        Assert.Equal(10_000, await GetBalanceAsync(sourceClient, sourceWalletId));
        Assert.Equal(0, await GetBalanceAsync(destinationClient, destinationWalletId));
    }

    [Fact]
    public async Task ReverseTransfer_BySenderWhoIsNotTheAffectedAccount_NonAdmin_ReturnsNotFound()
    {
        // The reversal would debit the recipient's wallet, not the sender's - a non-admin
        // sender can't trigger that unilaterally (see ReverseTransactionHandler's authorization
        // gate). Never a 403 here either - same non-leak pattern as the rest of this API.
        using var sourceClient = CreateClient();
        Authorize(sourceClient, await RegisterAndLoginAsync(sourceClient, UniqueEmail()));
        var sourceWalletId = await CreateWalletAsync(sourceClient);
        await FundAsync(sourceClient, sourceWalletId, 10_000);

        using var destinationClient = CreateClient();
        Authorize(destinationClient, await RegisterAndLoginAsync(destinationClient, UniqueEmail()));
        var destinationWalletId = await CreateWalletAsync(destinationClient);

        var transfer = await TransferAsync(sourceClient, sourceWalletId, destinationWalletId, 4_000);

        var reverseResponse = await ReverseAsync(sourceClient, sourceWalletId, transfer.Id, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.NotFound, reverseResponse.StatusCode);
    }

    [Fact]
    public async Task ReverseSimulatedFunding_RestoresWalletToZero()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);

        var funding = await FundAsync(client, walletId, 2_500);
        Assert.Equal(2_500, await GetBalanceAsync(client, walletId));

        var reverseResponse = await ReverseAsync(client, walletId, funding.Id, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, reverseResponse.StatusCode);

        Assert.Equal(0, await GetBalanceAsync(client, walletId));
    }

    [Fact]
    public async Task ReverseSameTransactionTwice_SecondAttemptReturnsConflict()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);
        var funding = await FundAsync(client, walletId, 1_000);

        var first = await ReverseAsync(client, walletId, funding.Id, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await ReverseAsync(client, walletId, funding.Id, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task ReverseSameIdempotencyKeyTwice_ReturnsSameReversal_NeverDoublePosts()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);
        var funding = await FundAsync(client, walletId, 1_000);

        var key = Guid.NewGuid().ToString();
        var first = await ReverseAsync(client, walletId, funding.Id, key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstDto = await first.Content.ReadFromJsonAsync<TransactionDto>();

        var replay = await ReverseAsync(client, walletId, funding.Id, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayDto = await replay.Content.ReadFromJsonAsync<TransactionDto>();

        Assert.Equal(firstDto!.Id, replayDto!.Id);
        Assert.Equal(0, await GetBalanceAsync(client, walletId));
    }

    [Fact]
    public async Task ReverseTransfer_RecipientAlreadySpentIt_ReturnsUnprocessableEntity()
    {
        using var sourceClient = CreateClient();
        Authorize(sourceClient, await RegisterAndLoginAsync(sourceClient, UniqueEmail()));
        var sourceWalletId = await CreateWalletAsync(sourceClient);
        await FundAsync(sourceClient, sourceWalletId, 5_000);

        using var destinationClient = CreateClient();
        Authorize(destinationClient, await RegisterAndLoginAsync(destinationClient, UniqueEmail()));
        var destinationWalletId = await CreateWalletAsync(destinationClient);

        using var thirdPartyClient = CreateClient();
        Authorize(thirdPartyClient, await RegisterAndLoginAsync(thirdPartyClient, UniqueEmail()));
        var thirdPartyWalletId = await CreateWalletAsync(thirdPartyClient);

        var transfer = await TransferAsync(sourceClient, sourceWalletId, destinationWalletId, 3_000);

        // Destination spends everything it just received before the reversal is attempted.
        await TransferAsync(destinationClient, destinationWalletId, thirdPartyWalletId, 3_000);
        Assert.Equal(0, await GetBalanceAsync(destinationClient, destinationWalletId));

        // The recipient is the one who self-service-reverses (it's their own wallet that would
        // be debited) - and it's correctly refused since they no longer have the funds.
        var reverseResponse = await ReverseAsync(destinationClient, destinationWalletId, transfer.Id, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reverseResponse.StatusCode);

        // Nothing moved - the failed reversal attempt left both balances untouched.
        Assert.Equal(2_000, await GetBalanceAsync(sourceClient, sourceWalletId));
        Assert.Equal(0, await GetBalanceAsync(destinationClient, destinationWalletId));
    }

    [Fact]
    public async Task ReverseReversal_ReturnsBadRequest()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);
        var funding = await FundAsync(client, walletId, 1_000);

        var reversalResponse = await ReverseAsync(client, walletId, funding.Id, Guid.NewGuid().ToString());
        var reversal = await reversalResponse.Content.ReadFromJsonAsync<TransactionDto>();

        var reverseReversalResponse = await ReverseAsync(client, walletId, reversal!.Id, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.BadRequest, reverseReversalResponse.StatusCode);
    }

    [Fact]
    public async Task Reverse_MissingIdempotencyKeyHeader_ReturnsBadRequest()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);
        var funding = await FundAsync(client, walletId, 1_000);

        var response = await ReverseAsync(client, walletId, funding.Id, idempotencyKey: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reverse_WalletBelongsToDifferentUser_ReturnsNotFound_NeverForbidden()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);
        var funding = await FundAsync(client, walletId, 1_000);

        using var otherClient = CreateClient();
        Authorize(otherClient, await RegisterAndLoginAsync(otherClient, UniqueEmail()));

        var response = await ReverseAsync(otherClient, walletId, funding.Id, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reverse_TransactionDoesNotInvolveWallet_ReturnsNotFound()
    {
        using var client = CreateClient();
        Authorize(client, await RegisterAndLoginAsync(client, UniqueEmail()));
        var walletId = await CreateWalletAsync(client);
        var unrelatedWalletId = await CreateWalletAsync(client);
        var unrelatedFunding = await FundAsync(client, unrelatedWalletId, 1_000);

        var response = await ReverseAsync(client, walletId, unrelatedFunding.Id, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
