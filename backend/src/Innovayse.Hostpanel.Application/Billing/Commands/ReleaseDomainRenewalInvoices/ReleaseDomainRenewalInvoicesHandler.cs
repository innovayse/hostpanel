using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ReleaseDomainRenewalInvoices;

/// <summary>
/// Takes a domain off every open auto-renewal invoice that carries it, returning any credit those
/// invoices had applied to the client's balance. An invoice that bills only this domain is
/// cancelled. One that also bills something else keeps the rest: while it is still editable (Draft
/// or Unpaid) only this domain's line is removed; once it is not (Overdue), it cannot be split and
/// the release is refused with <c>RENEWAL_INVOICE_OPEN</c> before anything changes. An invoice in
/// PaymentPending or Collections refuses the same way whether or not it bills anything else: a
/// gateway session may be in flight, or the debt is with collections, and cancelling under either
/// could lose a payment or double-charge. "Open" is the same set the renewal run uses
/// (<see cref="RenewalInvoiceStatuses.Open"/>). Paid invoices are never touched.
/// </summary>
/// <remarks>
/// Every invoice change and every credit return is saved in one <c>SaveChangesAsync</c>:
/// <see cref="IClientCredit"/> stages the balance change on this handler's own context, so the
/// invoices and the balances they return credit to commit or fail together.
/// </remarks>
public sealed class ReleaseDomainRenewalInvoicesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Returns released invoices' credit to the client's balance in the same save.</summary>
    private readonly IClientCredit _clientCredit;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<ReleaseDomainRenewalInvoicesHandler> _logger;

    /// <summary>The clock the moment of a cancellation is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Ends the consent recorded for the session of each invoice cancelled.</summary>
    private readonly UnboundConsentRevoker _consents;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clientCredit">Returns released invoices' credit to the client's balance in the same save.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The clock the moment of a cancellation is read from.</param>
    /// <param name="consents">Ends the consent recorded for the session of each invoice cancelled.</param>
    public ReleaseDomainRenewalInvoicesHandler(
        IHostpanelDbContext context,
        IClientCredit clientCredit,
        ILogger<ReleaseDomainRenewalInvoicesHandler> logger,
        IClock clock,
        UnboundConsentRevoker consents)
    {
        _context = context;
        _clientCredit = clientCredit;
        _logger = logger;
        _clock = clock;
        _consents = consents;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The domain to release.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, also when no open invoice bills the domain; <c>RENEWAL_INVOICE_OPEN</c> when an
    /// Overdue invoice also bills something else or a PaymentPending/Collections invoice bills this
    /// domain or one of them already took a payment; or <c>CLIENT_NOT_FOUND</c> when credit has no client to go back to. Nothing is saved
    /// on a refusal.
    /// </returns>
    public async Task<Result> HandleAsync(ReleaseDomainRenewalInvoicesCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var domainId = cmd.RegisteredDomainId;
        var openRenewals = await _context.Invoices
            .Include(i => i.Items)
            .Include(i => i.Transactions)
            .Where(i => RenewalInvoiceStatuses.Open.Contains(i.Status)
                        && i.Items.Any(l => l.RegisteredDomainId == domainId && l.RenewalPeriodStart != null))
            .ToListAsync(ct);
        if (openRenewals.Count == 0)
        {
            return Result.Success();
        }

        bool IsThisDomainsRenewal(InvoiceItem l) => l.RegisteredDomainId == domainId && l.RenewalPeriodStart != null;

        // Decided for every invoice before anything is changed, so a refusal leaves all of them as they were.
        var inFlight = openRenewals.FirstOrDefault(i => i.Status is InvoiceStatus.PaymentPending or InvoiceStatus.Collections);
        if (inFlight is not null)
        {
            _logger.LogInformation(
                "Release of domain {DomainId} refused: renewal invoice {InvoiceId} is {Status} and cannot be released.",
                domainId, inFlight.Id, inFlight.Status);
            return Result.FailureCode(HostpanelErrorCodes.RENEWAL_INVOICE_OPEN);
        }

        // Money already taken cannot follow a released line: cancelling would strand it, and dropping
        // a line could leave the payments above the new total. Staff refund or settle such an invoice.
        var partPaid = openRenewals.FirstOrDefault(i => i.PaymentsRecorded > 0);
        if (partPaid is not null)
        {
            _logger.LogInformation(
                "Release of domain {DomainId} refused: renewal invoice {InvoiceId} already took a payment.", domainId, partPaid.Id);
            return Result.FailureCode(HostpanelErrorCodes.RENEWAL_INVOICE_OPEN);
        }

        var mixedLocked = openRenewals.FirstOrDefault(i =>
            !i.Items.All(IsThisDomainsRenewal) && i.Status is not (InvoiceStatus.Draft or InvoiceStatus.Unpaid));
        if (mixedLocked is not null)
        {
            _logger.LogInformation(
                "Release of domain {DomainId} refused: overdue invoice {InvoiceId} also bills other items.", domainId, mixedLocked.Id);
            return Result.FailureCode(HostpanelErrorCodes.RENEWAL_INVOICE_OPEN);
        }

        // Credit goes back to the balance of the client each invoice was raised for.
        foreach (var open in openRenewals)
        {
            if (open.Items.All(IsThisDomainsRenewal))
            {
                // Credit first: a cancelled invoice keeps its Credit column, and money parked on a
                // cancelled invoice is money the client has lost.
                if (open.Credit > 0)
                {
                    var returned = await _clientCredit.ReturnToBalanceAsync(open, open.Credit, ct);
                    if (returned.IsFailure)
                    {
                        return returned;
                    }
                }

                open.Cancel(_clock.UtcNow);
                await _consents.RevokeOfAsync(open, ct);
                continue;
            }

            foreach (var line in open.Items.Where(IsThisDomainsRenewal).ToList())
            {
                open.RemoveItem(line.Id);
            }

            // The credit may now exceed what is left to pay; the excess goes back, never a negative total.
            if (open.ExcessCredit > 0)
            {
                var returned = await _clientCredit.ReturnToBalanceAsync(open, open.ExcessCredit, ct);
                if (returned.IsFailure)
                {
                    return returned;
                }
            }
        }

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
