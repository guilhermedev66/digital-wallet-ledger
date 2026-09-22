using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletLedger.Application.Dtos;
using WalletLedger.Application.Exceptions;
using WalletLedger.Application.Ledger;
using WalletLedger.Application.Wallets;
using WalletLedger.Domain.Entities;
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
    GetWalletHistoryHandler getWalletHistoryHandler,
    ReverseTransactionHandler reverseTransactionHandler,
    GetAccountReconciliationHandler getAccountReconciliationHandler) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<WalletDto>> Create(CreateWalletRequest request, CancellationToken ct)
    {
        if (!TryParseDefinedEnum<Currency>(request.Currency, out var currency))
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
    public async Task<ActionResult<PagedResult<TransactionDto>>> GetHistory(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? type = null,
        CancellationToken ct = default)
    {
        TransactionType? parsedType = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!TryParseDefinedEnum<TransactionType>(type, out var parsed))
            {
                return BadRequest(new { message = $"Unsupported transaction type '{type}'." });
            }

            parsedType = parsed;
        }

        try
        {
            var history = await getWalletHistoryHandler.HandleAsync(
                new GetWalletHistoryQuery(GetOwnerUserId(), id, page, pageSize, fromUtc, toUtc, parsedType), ct);

            // Same non-leak pattern as GetById/SimulateFunding/Transfer: not-found and not-yours are both 404.
            return history is null ? NotFound() : Ok(history);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/transactions/{transactionId:guid}/reverse")]
    public async Task<ActionResult<TransactionDto>> Reverse(
        Guid id, Guid transactionId, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new { message = "The 'Idempotency-Key' header is required." });
        }

        try
        {
            var reversal = await reverseTransactionHandler.HandleAsync(
                new ReverseTransactionCommand(GetOwnerUserId(), id, transactionId, idempotencyKey, CallerIsAdmin: User.IsInRole("admin")), ct);

            // Same non-leak pattern as GetById/SimulateFunding/Transfer: not-found, not-yours,
            // and "that transaction isn't yours to reverse" are all 404.
            return reversal is null ? NotFound() : Ok(reversal);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InsufficientFundsException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (TransactionAlreadyReversedException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (IdempotencyKeyConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/reconciliation")]
    public async Task<ActionResult<ReconciliationReportDto>> GetReconciliation(Guid id, CancellationToken ct)
    {
        var report = await getAccountReconciliationHandler.HandleAsync(
            new GetAccountReconciliationQuery(GetOwnerUserId(), id, CallerIsAdmin: User.IsInRole("admin")), ct);

        return report is null ? NotFound() : Ok(report);
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

    /// <summary>
    /// Enum.TryParse alone accepts comma-combined values on a non-[Flags] enum by OR-ing their
    /// underlying numbers - e.g. "SimulatedFunding,Reversal" parses to a numeric value (3) that
    /// matches no real row instead of failing. Enum.IsDefined alone doesn't fully close this:
    /// since one member is always numbered 0 (the OR identity), a combination that includes it
    /// - e.g. "Usd,Brl" (Usd=0) - ORs down to the OTHER member's own value and IS a defined
    /// member (Brl), so Enum.IsDefined can't tell it apart from someone just sending "Brl".
    /// Rejecting a comma outright closes it regardless of which member happens to be 0 - found
    /// and verified with a standalone repro (not guessed) during the M7 security gate pass.
    /// </summary>
    private static bool TryParseDefinedEnum<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;
        return value is not null && !value.Contains(',') && Enum.TryParse(value, ignoreCase: true, out result) && Enum.IsDefined(result);
    }
}

public sealed record CreateWalletRequest(string Currency, string? DisplayName);

public sealed record SimulateFundingRequest(long AmountMinorUnits);

public sealed record TransferRequest(Guid DestinationWalletId, long AmountMinorUnits);
