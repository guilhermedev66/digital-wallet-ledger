using System.Net;
using System.Net.Http.Json;
using WalletLedger.Api.Controllers;

namespace WalletLedger.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class AuthEndpointsTests(PostgresContainerFixture postgres)
{
    private HttpClient CreateClient() => new WalletLedgerApiFactory(postgres.ConnectionString).CreateClient();

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task Register_ThenLogin_Succeeds()
    {
        using var client = CreateClient();
        var email = UniqueEmail();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "correct horse battery staple"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "correct horse battery staple"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        using var client = CreateClient();
        var email = UniqueEmail();

        var first = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "correct horse battery staple"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "a-different-password"));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "correct horse battery staple"));

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "wrong password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized_SameAsWrongPassword()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(UniqueEmail(), "whatever"));

        // Same status as a wrong-password attempt against a real account - the response
        // never reveals whether the email is registered.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
