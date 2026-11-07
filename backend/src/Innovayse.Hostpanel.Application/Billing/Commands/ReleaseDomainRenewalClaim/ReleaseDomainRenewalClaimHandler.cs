using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ReleaseDomainRenewalClaim;

/// <summary>
/// Handles <see cref="ReleaseDomainRenewalClaimCommand"/>: clears the claim through
/// <see cref="Domain.Billing.Invoice.ReleaseRenewalClaim"/> and records who did it in the client's activity log,
/// saved together. The save carries the line's concurrency tokens, so a release that races the sweep loses
/// with a concurrency conflict instead of clearing a claim another runner has just taken.
/// </summary>
public sealed class ReleaseDomainRenewalClaimHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Records the activity log line, saved with the release.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<ReleaseDomainRenewalClaimHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="activity">Records the activity log line, saved with the release.</param>
    /// <param name="logger">Structured logger.</param>
    public ReleaseDomainRenewalClaimHandler(IHostpanelDbContext context, IActivityLogger activity, ILogger<ReleaseDomainRenewalClaimHandler> logger)
    {
        _context = context;
        _activity = activity;
        _logger = logger;
    }

    /// <summary>Releases the claim.</summary>
    /// <param name="cmd">The invoice and the line.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, <c>INVOICE_NOT_FOUND</c>, <c>INVOICE_ITEM_NOT_FOUND</c> when the invoice has no such line, or
    /// <c>RENEWAL_CLAIM_NOT_HELD</c> when the line is not a claimed, unapplied domain renewal.
    /// </returns>
    public async Task<Result> HandleAsync(ReleaseDomainRenewalClaimCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var invoice = await _context.Invoices.Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == cmd.InvoiceId, ct);
        if (invoice is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        var domainId = invoice.Items.FirstOrDefault(l => l.Id == cmd.ItemId)?.RegisteredDomainId;
        invoice.ReleaseRenewalClaim(cmd.ItemId);

        await _activity.RecordAsync(
            new ActivityLogEntry(
                invoice.ClientId,
                $"Released Domain Renewal Claim - Invoice ID: {invoice.Id}, Item ID: {cmd.ItemId}, Domain ID: {domainId}"),
            ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Renewal claim on invoice {InvoiceId} line {ItemId} (domain {DomainId}) released by staff; the next sweep retries it.",
            invoice.Id, cmd.ItemId, domainId);
        return Result.Success();
    }
}
