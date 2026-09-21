using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletLedger.Application.Dtos;
using WalletLedger.Application.Exceptions;
using WalletLedger.Application.Ledger;
using WalletLedger.Application.Wallets;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/wallets")]
public sealed class WalletsController(
    CreateWalletHandler createWalletHandler,
    ListMyWalletsHandler listMyWalletsHandler,
    GetWalletByIdHandler getWalletByIdHandler,
    SimulateFundingHandler simulateFundingHandler,
    GetWalletBalanceHandler getWalletBalanceHandler,
    TransferHandler transferHandler,
    GetWalletHistoryHandler getWalletHistoryHandler) : ControllerBase
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
        var wallet = await getWalletByIdHandler.HandleAsync(
            new GetWalletByIdQuery(GetOwnerUserId(), id, CallerIsAdmin: User.IsInRole("admin")), ct);

        // Not found and "not yours" are indistinguishable here on purpose - both 404,
        // never 403, so a non-owner can't use this endpoint to probe wallet existence.
        return wallet is null ? NotFound() : Ok(wallet);
    }

    [HttpPost("{id:guid}/simulate-funding")]
    public async Task<ActionResult<TransactionDto>> SimulateFunding(
        Guid id, SimulateFundingRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new { message = "The 'Idempotency-Key' header is required." });
        }

        try
        {
            var transaction = await simulateFundingHandler.HandleAsync(
                new SimulateFundingCommand(GetOwnerUserId(), id, request.AmountMinorUnits, idempotencyKey), ct);

            // Same non-leak pattern as GetById: not-found and not-yours are both 404.
            return transaction is null ? NotFound() : Ok(transaction);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (IdempotencyKeyConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/balance")]
    public async Task<ActionResult<WalletBalanceDto>> GetBalance(Guid id, CancellationToken ct)
    {
        var balance = await getWalletBalanceHandler.HandleAsync(new GetWalletBalanceQuery(GetOwnerUserId(), id), ct);
        return balance is null ? NotFound() : Ok(balance);
    }

    [HttpPost("{id:guid}/transfer")]
    public async Task<ActionResult<TransactionDto>> Transfer(
        Guid id, TransferRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new { message = "The 'Idempotency-Key' header is required." });
        }

        try
        {
            var transaction = await transferHandler.HandleAsync(
                new TransferCommand(GetOwnerUserId(), id, request.DestinationWalletId, request.AmountMinorUnits, idempotencyKey), ct);

            // Same non-leak pattern as GetById/SimulateFunding: not-found and not-yours (for
            // the SOURCE wallet only) are both 404. A bad destination is a distinct 400 - see
            // TransferHandler's doc comment for why that's not the same kind of leak.
            return transaction is null ? NotFound() : Ok(transaction);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InsufficientFundsException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (IdempotencyKeyConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<PagedResult<TransactionDto>>> GetHistory(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            var history = await getWalletHistoryHandler.HandleAsync(new GetWalletHistoryQuery(GetOwnerUserId(), id, page, pageSize), ct);

            // Same non-leak pattern as GetById/SimulateFunding/Transfer: not-found and not-yours are both 404.
            return history is null ? NotFound() : Ok(history);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
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

public sealed record SimulateFundingRequest(long AmountMinorUnits);

public sealed record TransferRequest(Guid DestinationWalletId, long AmountMinorUnits);
