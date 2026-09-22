using System.Net;
using System.Net.Http;

namespace WalletLedger.IntegrationTests;

/// <summary>M7: baseline security headers on every response, and a restrictive CORS policy (allowed origins from config, never AllowAnyOrigin - see Program.cs).</summary>
[Collection(PostgresCollection.Name)]
public sealed class SecurityHeadersAndCorsTests(PostgresContainerFixture postgres)
{
    private HttpClient CreateClient() => new WalletLedgerApiFactory(postgres.ConnectionString).CreateClient();

    [Fact]
    public async Task AnyResponse_CarriesBaselineSecurityHeaders()
    {
        using var client = CreateClient();

        // Unauthenticated - a plain, ordinary response through the normal (non-exception) pipeline.
        var response = await client.GetAsync("/api/wallets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var contentTypeOptions));
        Assert.Equal("nosniff", contentTypeOptions!.Single());
        Assert.True(response.Headers.TryGetValues("Referrer-Policy", out var referrerPolicy));
        Assert.Equal("no-referrer", referrerPolicy!.Single());
    }

    [Fact]
    public async Task Cors_PreflightFromConfiguredOrigin_IsAllowed()
    {
        // appsettings.Development.json's Cors:AllowedOrigins includes the Vite dev server origin -
        // WebApplicationFactory defaults to the Development environment, so that file is in play.
        using var client = CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/wallets");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigin));
        Assert.Equal("http://localhost:5173", allowedOrigin!.Single());
    }

    [Fact]
    public async Task Cors_PreflightFromUnconfiguredOrigin_IsNotAllowed()
    {
        using var client = CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/wallets");
        request.Headers.Add("Origin", "http://evil.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        // No CORS headers at all for an origin that isn't on the allowlist - never
        // AllowAnyOrigin(), see MEMORY.md and Program.cs's AddCors policy.
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
