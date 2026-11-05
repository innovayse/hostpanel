using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The question "is a stored card charge of this invoice still unfinished", shared by the cancellation and the
/// manual payment, so the two can never disagree on it; and the wider question "does a claim block a new hosted
/// payment of it", asked by the hosted gateway start.
/// </summary>
/// <remarks>
/// <see cref="HasUnresolvedClaimAsync"/> leaves a held claim out: the money is taken and sits with an operator, who
/// settles it by recording a payment by hand or by cancelling the invoice. <see cref="BlockingClaimErrorAsync"/> counts a
/// held claim too (<see cref="InvoicePaymentClaim.BlockingFilter"/>), so the payer is never asked to pay again while
/// money is held.
/// </remarks>
public static class InvoicePaymentClaimQueryExtensions
{
    /// <summary>
    /// Whether the invoice has a claim that has not ended (<see cref="InvoicePaymentClaim.UnresolvedFilter"/>): a stored
    /// charge that is in flight, waiting for the payer, or accepted by the gateway and not yet settled.
    /// </summary>
    /// <param name="claims">The invoice payment claims.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the invoice must not be cancelled or paid by hand yet.</returns>
    public static Task<bool> HasUnresolvedClaimAsync(
        this IQueryable<InvoicePaymentClaim> claims, int invoiceId, CancellationToken ct) =>
        claims
            .Where(c => c.InvoiceId == invoiceId)
            .Where(InvoicePaymentClaim.UnresolvedFilter)
            .AnyAsync(ct);

    /// <summary>
    /// The error code that explains why a claim of the invoice stops a new hosted payment session
    /// (<see cref="InvoicePaymentClaim.BlockingFilter"/>): an unfinished charge first, a held one otherwise.
    /// </summary>
    /// <param name="claims">The invoice payment claims.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>INVOICE_STORED_CHARGE_UNRESOLVED</c> when a charge has not ended, <c>INVOICE_STORED_CHARGE_HELD</c> when only
    /// a held claim blocks, or <see langword="null"/> when nothing blocks.
    /// </returns>
    public static async Task<string?> BlockingClaimErrorAsync(
        this IQueryable<InvoicePaymentClaim> claims, int invoiceId, CancellationToken ct)
    {
        var statuses = await claims
            .Where(c => c.InvoiceId == invoiceId)
            .Where(InvoicePaymentClaim.BlockingFilter)
            .Select(c => c.Status)
            .ToListAsync(ct);
        if (statuses.Count == 0)
        {
            return null;
        }

        return statuses.Any(status => status != InvoicePaymentClaimStatus.Held)
            ? HostpanelErrorCodes.INVOICE_STORED_CHARGE_UNRESOLVED
            : HostpanelErrorCodes.INVOICE_STORED_CHARGE_HELD;
    }
}
