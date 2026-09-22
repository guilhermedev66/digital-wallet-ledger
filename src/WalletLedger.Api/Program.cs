using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

app.UseHttpsRedirection();

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
