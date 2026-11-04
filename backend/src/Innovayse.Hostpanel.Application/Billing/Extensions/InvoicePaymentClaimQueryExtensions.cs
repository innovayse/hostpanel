using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The one question "is a stored card charge of this invoice still unfinished", shared by the cancellation and the
/// manual payment, so the two can never disagree on it.
/// </summary>
/// <remarks>
/// A held claim is deliberately not counted here: the money is taken and sits with an operator, who settles it by
/// recording a payment by hand or by cancelling the invoice. Only the automatic charge treats a held claim as blocking
/// (<see cref="InvoicePaymentClaim.BlockingFilter"/>).
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
}
