using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletLedger.Application.Dtos;
using WalletLedger.Application.Ledger;

namespace WalletLedger.Api.Controllers;

/// <summary>System-wide reconciliation, across every account, not just the caller's own - see GetGlobalReconciliationHandler for why this is admin-gated. Per-wallet reconciliation is WalletsController's ownership-scoped `GET /api/wallets/{id}/reconciliation` instead.</summary>
[ApiController]
[Authorize]
[Route("api/reconciliation")]
public sealed class ReconciliationController(GetGlobalReconciliationHandler getGlobalReconciliationHandler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ReconciliationReportDto>> GetGlobal(CancellationToken ct)
    {
        var report = await getGlobalReconciliationHandler.HandleAsync(new GetGlobalReconciliationQuery(User.IsInRole("admin")), ct);

        // Never a 403 anywhere in this API (see MEMORY.md) - a non-admin caller gets the same
        // 404 as any other "you can't do that" case here, not a signal that this route exists.
        return report is null ? NotFound() : Ok(report);
    }
}
