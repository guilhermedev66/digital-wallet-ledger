using System.Net;
using System.Net.Http.Json;
using WalletLedger.Api.Controllers;

namespace WalletLedger.IntegrationTests;

/// <summary>M7: unlimited login/register attempts would allow credential stuffing / password guessing - see Program.cs's AddRateLimiter("auth") policy, applied to AuthController via [EnableRateLimiting].</summary>
[Collection(PostgresCollection.Name)]
public sealed class AuthRateLimitingTests(PostgresContainerFixture postgres)
{
    private HttpClient CreateClient() => new WalletLedgerApiFactory(postgres.ConnectionString).CreateClient();

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task Login_ExceedsPermitLimitWithinWindow_ReturnsTooManyRequests()
    {
        // The policy allows 10 requests/minute per (in this in-memory TestServer) shared
        // partition key - firing 11 in a row from one client must trip it. Each request
        // targets a different unknown email so every one legitimately reaches the rate
        // limiter (rather than short-circuiting earlier in the pipeline for an unrelated reason).
        using var client = CreateClient();

        HttpStatusCode? lastStatus = null;
        for (var i = 0; i < 11; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(UniqueEmail(), "whatever"));
            lastStatus = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
    }

    [Fact]
    public async Task Login_WithinPermitLimit_NeverRateLimited()
    {
        using var client = CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(UniqueEmail(), "whatever"));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task RateLimit_IsIsolatedPerHost_DoesNotLeakBetweenTestFactories()
    {
        // Each WalletLedgerApiFactory is a fresh in-memory host with its own rate limiter
        // state - a prior test's requests must not count against this one's quota.
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(UniqueEmail(), "whatever"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
