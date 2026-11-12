using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Events;

/// <summary>
/// Ends the consent recorded for the hosted session of an invoice once the invoice is paid, whichever path paid it (a
/// staff payment, credit, a bank transfer, a stored charge or the session itself): <c>Invoice.ApplyPaid</c> is the one
/// place an invoice turns paid and it raises <see cref="PaymentReceivedEvent"/> in the same save, so this is the one
/// place the consent of a session that did not carry the payment is closed (<see cref="UnboundConsentRevoker"/>). A consent
/// the session's own payment bound to a method was bound in that same save and is not touched. A non-notification
/// handler, and safe as one: revoking is idempotent, and a failure is retried by the queue.
/// </summary>
public sealed class EndSessionConsentOnPaymentReceivedHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Ends the consent recorded for the invoice's session.</summary>
    private readonly UnboundConsentRevoker _consents;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="consents">Ends the consent recorded for the invoice's session.</param>
    public EndSessionConsentOnPaymentReceivedHandler(IHostpanelDbContext context, UnboundConsentRevoker consents)
    {
        _context = context;
        _consents = consents;
    }

    /// <summary>Revokes the unbound consent of the paid invoice's session.</summary>
    /// <param name="evt">The payment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the consent is revoked and saved, or at once when there is none.</returns>
    public async Task HandleAsync(PaymentReceivedEvent evt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var invoice = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == evt.InvoiceId, ct);
        if (invoice is null)
        {
            return;
        }

        await _consents.RevokeOfAsync(invoice, ct);
        await _context.SaveChangesAsync(ct);
    }
}
