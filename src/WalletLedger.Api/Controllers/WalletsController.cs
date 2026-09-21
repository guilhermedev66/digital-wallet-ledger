using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletLedger.Application.Dtos;
using WalletLedger.Application.Wallets;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/wallets")]
public sealed class WalletsController(
    CreateWalletHandler createWalletHandler,
    ListMyWalletsHandler listMyWalletsHandler,
    GetWalletByIdHandler getWalletByIdHandler) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<WalletDto>> Create(CreateWalletRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<Currency>(request.Currency, ignoreCase: true, out var currency))
        {
            return BadRequest(new { message = $"Unsupported currency '{request.Currency}'." });
        }

        var wallet = await createWalletHandler.HandleAsync(new CreateWalletCommand(GetOwnerUserId(), currency, request.DisplayName), ct);

        return CreatedAtAction(nameof(GetById), new { id = wallet.Id }, wallet);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WalletDto>>> ListMine(CancellationToken ct)
    {
        var wallets = await listMyWalletsHandler.HandleAsync(new ListMyWalletsQuery(GetOwnerUserId()), ct);
        return Ok(wallets);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WalletDto>> GetById(Guid id, CancellationToken ct)
    {
        var wallet = await getWalletByIdHandler.HandleAsync(new GetWalletByIdQuery(GetOwnerUserId(), id), ct);

        // Not found and "not yours" are indistinguishable here on purpose - both 404,
        // never 403, so a non-owner can't use this endpoint to probe wallet existence.
        return wallet is null ? NotFound() : Ok(wallet);
    }

    /// <summary>Owner id is always resolved from the authenticated caller's own JWT claims - never from client input.</summary>
    private Guid GetOwnerUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (sub is null || !Guid.TryParse(sub, out var userId))
        {
            throw new InvalidOperationException("Authenticated request is missing a valid 'sub' claim.");
        }

        return userId;
    }
}

public sealed record CreateWalletRequest(string Currency, string? DisplayName);
