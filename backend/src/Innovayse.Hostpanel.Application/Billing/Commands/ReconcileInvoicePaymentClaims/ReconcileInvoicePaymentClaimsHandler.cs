using Innovayse.Hostpanel.Application.Billing.Commands.ApplyStoredChargeOutcome;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Hostpanel.Sdk.Plugins.StoredCredentials;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ReconcileInvoicePaymentClaims;

/// <summary>
/// Handles <see cref="ReconcileInvoicePaymentClaimsCommand"/>: resolves the stored charge claims that are still
/// unresolved after <see cref="InvoicePaymentClaim.StaleAfter"/> by asking their plugin about the idempotency key.
/// </summary>
/// <remarks>
/// <para>
/// The pass checks no consent, no client status and no invoice state: a charge that was made must be recorded, and
/// whether a new one would be allowed is not the question. What it found goes to
/// <see cref="ApplyStoredChargeOutcomeCommand"/>, which records a success, keeps a pending or authenticating charge
/// open and ends a key the gateway never charged under as abandoned. A lookup that cannot be answered (the gateway is
/// down, the module was switched off) changes nothing: the claim is looked at again after the next wait, and a claim
/// is never abandoned on a lookup that failed.
/// </para>
/// <para>
/// One bad claim is logged and counted as looked at; it never stops the pass.
/// </para>
/// </remarks>
public sealed class ReconcileInvoicePaymentClaimsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the stored credential capability of a payment gateway module.</summary>
    private readonly IStoredCredentialPluginResolver _plugins;

    /// <summary>The Wolverine bus the outcome command is dispatched through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<ReconcileInvoicePaymentClaimsHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="plugins">Resolves the stored credential capability of a payment gateway module.</param>
    /// <param name="bus">The Wolverine bus the outcome command is dispatched through.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    /// <param name="logger">Structured logger.</param>
    public ReconcileInvoicePaymentClaimsHandler(
        IHostpanelDbContext context,
        IStoredCredentialPluginResolver plugins,
        IMessageBus bus,
        IClock clock,
        ILogger<ReconcileInvoicePaymentClaimsHandler> logger)
    {
        _context = context;
        _plugins = plugins;
        _bus = bus;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of claims that were looked at in this pass.</returns>
    public async Task<Result<int>> HandleAsync(ReconcileInvoicePaymentClaimsCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var now = _clock.UtcNow;
        var staleBefore = now - InvoicePaymentClaim.StaleAfter;

        // Read-only here; the outcome command reloads and mutates the claim in its own scope. The age test is the
        // query form of InvoicePaymentClaim.IsStale.
        var stale = await _context.InvoicePaymentClaims
            .AsNoTracking()
            .Where(InvoicePaymentClaim.UnresolvedFilter)
            .Where(c => (c.CheckedAt ?? c.CreatedAt) <= staleBefore)
            .OrderBy(c => c.Id)
            .Take(JobBatch.Size)
            .ToListAsync(ct);

        foreach (var claim in stale)
        {
            try
            {
                await ReconcileAsync(claim, ct);
            }
            catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
            {
                _logger.LogError(ex, "Reconciling stored charge claim {ClaimId} of invoice {InvoiceId} failed; the next pass retries it.", claim.Id, claim.InvoiceId);
            }
        }

        return Result.Success(stale.Count);
    }

    /// <summary>Asks the plugin about one claim's key and applies the answer.</summary>
    /// <param name="claim">The stale claim.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the answer is applied or the claim is left for the next pass.</returns>
    private async Task ReconcileAsync(InvoicePaymentClaim claim, CancellationToken ct)
    {
        var plugin = await _plugins.ResolveAsync(claim.GatewayModuleId, ct);
        var customerRef = await _context.GatewayCustomers
            .AsNoTracking()
            .Where(c => c.ClientId == claim.ClientId && c.GatewayModuleId == claim.GatewayModuleId)
            .Select(c => c.CustomerRef)
            .FirstOrDefaultAsync(ct);
        if (plugin is null || customerRef is null)
        {
            _logger.LogWarning(
                "Stored charge claim {ClaimId} of invoice {InvoiceId} cannot be looked up: module {Module} offers no capability or the customer is unknown; left unresolved.",
                claim.Id, claim.InvoiceId, claim.GatewayModuleId);
            return;
        }

        var lookup = await plugin.FindChargeAsync(customerRef, claim.IdempotencyKey, ct);
        if (lookup.Found && lookup.Outcome is null)
        {
            _logger.LogError(
                "Module {Module} reported a charge for claim {ClaimId} but no outcome; left unresolved.", claim.GatewayModuleId, claim.Id);
            return;
        }

        // Found = false is trusted: the lookup itself succeeded, or it would have thrown.
        await _bus.InvokeAsync<Result<InvoiceChargeDto>>(
            new ApplyStoredChargeOutcomeCommand(
                claim.Id,
                lookup.Found ? lookup.Outcome : null,
                lookup.TransactionId,
                lookup.AmountMinor,
                lookup.Currency,
                lookup.ActionUrl),
            ct);
    }
}
