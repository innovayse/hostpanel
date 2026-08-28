namespace Innovayse.Application.Billing.Common;

/// <summary>
/// Thrown when a client-facing invoice use case is asked for an invoice the caller may not have.
/// <para>
/// One type deliberately covers three situations: the invoice belongs to another client, no
/// invoice with that id exists at all, and the caller has no client record. Invoice ids are
/// sequential integers, so answering them apart -- 403 here, 404 there -- would turn
/// <c>/api/me/invoices/{id}</c> into a way of finding out which ids are real and which of them
/// are somebody else's. All three answer 404 carrying <see cref="Code"/>.
/// </para>
/// <para>
/// It is the counterpart of <c>TicketNotFoundException</c>: the two client-facing areas that
/// address a resource by sequential id answer the same shape, rather than each inventing one.
/// </para>
/// </summary>
/// <param name="invoiceId">The invoice id that was asked for. For the server-side log only.</param>
public sealed class InvoiceNotFoundException(int invoiceId) : Exception(PublicMessage)
{
    /// <summary>
    /// Machine-readable code sent to the caller as the <c>code</c> field of the error body.
    /// SCREAMING_SNAKE, the casing every error code on this platform uses. The frontend branches
    /// on this string, so it is part of the wire contract and must not be reworded.
    /// </summary>
    public const string Code = "INVOICE_NOT_FOUND";

    /// <summary>
    /// The sentence the caller is shown. It deliberately says nothing about whether the invoice
    /// exists or who it belongs to -- that is the fact this type exists to withhold.
    /// </summary>
    public const string PublicMessage = "No such invoice.";

    /// <summary>
    /// The invoice id that was asked for. Logged server-side; never written to the response body,
    /// because echoing it back tells the caller their probe was understood.
    /// </summary>
    public int InvoiceId { get; } = invoiceId;
}
