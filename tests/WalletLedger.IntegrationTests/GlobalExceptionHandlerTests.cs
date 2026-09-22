using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WalletLedger.Api.Controllers;
using WalletLedger.Application.Abstractions;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.IntegrationTests;

/// <summary>M7: an unhandled exception must produce a clean RFC 7807 ProblemDetails body - never a stack trace or the exception's own message, which could disclose internals to a client. See Program.cs's app.UseExceptionHandler.</summary>
[Collection(PostgresCollection.Name)]
public sealed class GlobalExceptionHandlerTests(PostgresContainerFixture postgres)
{
    private sealed class ThrowingUserRepository : IUserRepository
    {
        public const string ExceptionMessage = "boom-test-exception-should-never-reach-the-client";

        public Task<User?> GetByEmailAsync(Email email, CancellationToken ct) => throw new InvalidOperationException(ExceptionMessage);

        public Task AddAsync(User user, CancellationToken ct) => throw new InvalidOperationException(ExceptionMessage);
    }

    [Fact]
    public async Task UnhandledException_ReturnsCleanProblemDetails_NoStackTraceOrExceptionMessage()
    {
        using var factory = new WalletLedgerApiFactory(postgres.ConnectionString, services =>
        {
            services.RemoveAll<IUserRepository>();
            services.AddScoped<IUserRepository, ThrowingUserRepository>();
        });
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest($"{Guid.NewGuid():N}@example.com", "whatever"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(ThrowingUserRepository.ExceptionMessage, body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("   at ", body); // .NET stack trace line prefix
        Assert.DoesNotContain("WalletLedger.", body); // no internal namespace/type names
    }

    [Fact]
    public async Task UnhandledException_StillCarriesBaselineSecurityHeaders()
    {
        // Real gap found while manually smoke-testing this middleware: ExceptionHandlerMiddleware
        // clears the response (including headers already set by earlier middleware) before
        // re-executing its branch, so headers added only in the normal pipeline silently
        // vanished from a 500 response until Program.cs set them explicitly in both places.
        using var factory = new WalletLedgerApiFactory(postgres.ConnectionString, services =>
        {
            services.RemoveAll<IUserRepository>();
            services.AddScoped<IUserRepository, ThrowingUserRepository>();
        });
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest($"{Guid.NewGuid():N}@example.com", "whatever"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var contentTypeOptions));
        Assert.Equal("nosniff", contentTypeOptions!.Single());
        Assert.True(response.Headers.TryGetValues("Referrer-Policy", out var referrerPolicy));
        Assert.Equal("no-referrer", referrerPolicy!.Single());
    }
}
