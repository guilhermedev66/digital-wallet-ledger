using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Identity;
using WalletLedger.Application.Ledger;
using WalletLedger.Application.Wallets;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;
using WalletLedger.Infrastructure.Persistence;
using WalletLedger.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// RFC 7807 ProblemDetails for every error response, including the global exception handler
// below - never an exception's message or stack trace, see that handler's own comment.
builder.Services.AddProblemDetails();

// Anonymous credential endpoints only (see AuthController's [EnableRateLimiting("auth")]) -
// unlimited login/register attempts today would allow credential stuffing / password
// guessing. Partitioned by remote IP so one abusive client can't exhaust everyone else's
// quota; a generous-enough window that legitimate retries (e.g. a mistyped password) aren't
// punished. Not a distributed limiter - single-instance portfolio deployment, see MEMORY.md.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// Restrictive by default (see MEMORY.md) - only origins explicitly listed in
// Cors:AllowedOrigins may call this API cross-origin; an empty/unconfigured list (the
// production default - no real deploy URL exists yet) means no browser origin is allowed at
// all, never AllowAnyOrigin().
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddDbContext<WalletLedgerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Connection string 'ConnectionStrings:Default' is not configured.")));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IWalletRepository, EfWalletRepository>();
builder.Services.AddScoped<ITransactionRepository, EfTransactionRepository>();
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

builder.Services.AddScoped<RegisterUserHandler>();
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<CreateWalletHandler>();
builder.Services.AddScoped<ListMyWalletsHandler>();
builder.Services.AddScoped<GetWalletByIdHandler>();
builder.Services.AddScoped<SimulateFundingHandler>();
builder.Services.AddScoped<GetWalletBalanceHandler>();
builder.Services.AddScoped<TransferHandler>();
builder.Services.AddScoped<GetWalletHistoryHandler>();
builder.Services.AddScoped<ReverseTransactionHandler>();
builder.Services.AddScoped<GetAccountReconciliationHandler>();
builder.Services.AddScoped<GetGlobalReconciliationHandler>();

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtSigningKey = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Configuration 'Jwt:SigningKey' is not set.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim types exactly as issued ("sub", "email") instead of ASP.NET Core's
        // legacy inbound mapping to long XML-namespaced ClaimTypes.* equivalents.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = "role",
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Minimal, framework-provided-equivalent baseline headers for a JSON API - no CSP (this API
// never serves HTML, and a CSP tuned for a frontend that doesn't exist here yet would be
// guesswork) and no extra NuGet dependency for two headers. Applied explicitly in BOTH the
// normal pipeline below and inside the exception handler branch further down, not just placed
// early and left to flow through - ExceptionHandlerMiddleware clears the response (including
// any headers already set) before re-executing its branch on an unhandled exception, so a 500
// response would otherwise ship without these. Found by actually running the app and diffing
// headers on a normal 401 vs. a forced 500 - the 401 had them, the 500 didn't, until this was
// made explicit in both places.
app.Use(async (context, next) =>
{
    AddSecurityHeaders(context);
    await next();
});

// First in the pipeline so it catches an unhandled exception from anything downstream.
// Results.Problem() with no arguments is deliberately the generic RFC 7807 body ASP.NET Core
// ships (status 500, a fixed generic title/detail) - never the exception's own Message or
// StackTrace, which could disclose internals to a client. See GlobalExceptionHandlerTests.
app.UseExceptionHandler(exceptionHandlerApp =>
    exceptionHandlerApp.Run(async context =>
    {
        AddSecurityHeaders(context);
        await Results.Problem().ExecuteAsync(context);
    }));

// UseHsts excludes localhost/loopback automatically, but still gate on non-Development so a
// local `dotnet run` over plain HTTP is never told to force HTTPS on itself.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseCors("frontend");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

try
{
    await SeedSystemFundingAccountsAsync(app.Services);
}
catch (Exception ex)
{
    // Best-effort: don't let an unreachable database at boot (e.g. no local Postgres -
    // this WSL distro has no Docker daemon, see MEMORY.md) crash the whole process. Every
    // other endpoint keeps working; simulated-funding will fail loudly with its own
    // "no system funding account configured" error until this is resolved and the app
    // restarts (or a later request happens to succeed once the DB comes back).
    app.Logger.LogWarning(ex, "Could not seed system funding accounts at startup - simulated funding will fail until this is resolved.");
}

app.Run();

static void AddSecurityHeaders(HttpContext context)
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
}

// One SystemFunding LedgerAccount per supported currency, created out-of-band at boot -
// never reachable through any client-facing endpoint (see LedgerAccount.OpenSystemFundingAccount).
static async Task SeedSystemFundingAccountsAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var walletRepository = scope.ServiceProvider.GetRequiredService<IWalletRepository>();

    foreach (var currency in Enum.GetValues<Currency>())
    {
        var existing = await walletRepository.GetSystemFundingAccountAsync(currency, CancellationToken.None);
        if (existing is null)
        {
            var account = LedgerAccount.OpenSystemFundingAccount(currency, $"{currency} Simulated Funding Source");
            await walletRepository.AddAsync(account, CancellationToken.None);
        }
    }
}

/// <summary>Entry point marker for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
