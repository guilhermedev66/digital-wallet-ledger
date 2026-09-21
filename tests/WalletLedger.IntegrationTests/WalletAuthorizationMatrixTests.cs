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

/// <summary>
/// ROADMAP.md M1: "Authorization matrix tests: anonymous, owner, non-owner, admin."
/// GetById returns 404 for both "doesn't exist" and "not yours" - there is no 403 anywhere
/// in this design, since a 403 would itself leak that the wallet exists. Admin bypasses
/// ownership entirely.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class WalletAuthorizationMatrixTests(PostgresContainerFixture postgres)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";
    private const string Password = "correct horse battery staple";

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string email)
    {
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, Password));
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResultDto>();
        return auth!.AccessToken;
    }

    private static void Authorize(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task AuthorizationMatrix_AnonymousOwnerNonOwnerAdmin()
    {
        using var factory = new WalletLedgerApiFactory(postgres.ConnectionString);

        // Owner creates a wallet.
        var ownerEmail = UniqueEmail();
        using var ownerClient = factory.CreateClient();
        var ownerToken = await RegisterAndLoginAsync(ownerClient, ownerEmail);
        Authorize(ownerClient, ownerToken);
        var createResponse = await ownerClient.PostAsJsonAsync("/api/wallets", new CreateWalletRequest("Usd", "Owner's wallet"));
        var wallet = await createResponse.Content.ReadFromJsonAsync<WalletDto>();
        var walletId = wallet!.Id;

        // Anonymous: no Authorization header at all.
        using var anonymousClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.GetAsync("/api/wallets")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.GetAsync($"/api/wallets/{walletId}")).StatusCode);

        // Owner: full access to their own wallet.
        var ownerGet = await ownerClient.GetAsync($"/api/wallets/{walletId}");
        Assert.Equal(HttpStatusCode.OK, ownerGet.StatusCode);
        var ownerFetched = await ownerGet.Content.ReadFromJsonAsync<WalletDto>();
        Assert.Equal(walletId, ownerFetched!.Id);

        // Non-owner: a second, unrelated registered user. Must get 404, never 403 -
        // a 403 here would itself leak that the wallet exists.
        var nonOwnerEmail = UniqueEmail();
        using var nonOwnerClient = factory.CreateClient();
        var nonOwnerToken = await RegisterAndLoginAsync(nonOwnerClient, nonOwnerEmail);
        Authorize(nonOwnerClient, nonOwnerToken);
        var nonOwnerGet = await nonOwnerClient.GetAsync($"/api/wallets/{walletId}");
        Assert.Equal(HttpStatusCode.NotFound, nonOwnerGet.StatusCode);

        // Admin: a third user, promoted out-of-band (no self-service API for this),
        // must log in again to pick up the "role":"admin" claim, then bypasses ownership.
        var adminEmail = UniqueEmail();
        using var adminClient = factory.CreateClient();
        await RegisterAndLoginAsync(adminClient, adminEmail);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<WalletLedgerDbContext>();
            var adminUser = await dbContext.Users.SingleAsync(u => u.Email == Email.Parse(adminEmail));
            adminUser.PromoteToAdmin();
            await dbContext.SaveChangesAsync();
        }

        // Re-login: the JWT only carries "role":"admin" for whatever IsAdmin was at login time.
        var adminToken = await RegisterAndLoginAsync(adminClient, adminEmail);
        Authorize(adminClient, adminToken);

        var adminGet = await adminClient.GetAsync($"/api/wallets/{walletId}");
        Assert.Equal(HttpStatusCode.OK, adminGet.StatusCode);
        var adminFetched = await adminGet.Content.ReadFromJsonAsync<WalletDto>();
        Assert.Equal(walletId, adminFetched!.Id);
        Assert.Equal(wallet.OwnerUserId, adminFetched.OwnerUserId);
    }
}
