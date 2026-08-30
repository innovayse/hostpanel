namespace Innovayse.Application.Billing.Queries.GetMyServiceInvoices;

using Innovayse.Application.Billing.Common;

/// <summary>
/// What this platform can and cannot say about the money charged for one service.
/// </summary>
/// <remarks>
/// <para>
/// Two fields rather than a bare list, because a bare list cannot be read honestly. An empty
/// list means "no invoice line is recorded against this service" — and that is <b>not</b> the
/// same claim as "this service was never charged". Every invoice raised before
/// <c>invoice_items.client_service_id</c> existed carries no link, no backfill was written for
/// them, and none could be: an invoice line is a description, a unit price and a quantity, so
/// inferring which service an old line was for would be a guess rendered as fact on a page a
/// customer uses to check a charge.
/// </para>
/// <para>
/// <see cref="UnattributedInvoiceCount"/> is therefore reported alongside, and it is a count of
/// the caller's <b>own</b> invoices that carry no service link on any line — not a claim that any
/// of them concern this service. The portal renders it as "N charges are not recorded against
/// any service" rather than folding it into an empty state.
/// </para>
/// </remarks>
/// <param name="Invoices">
/// The invoices that carry at least one line explicitly charged to this service, newest first.
/// </param>
/// <param name="UnattributedInvoiceCount">
/// How many of the caller's invoices carry no service link on any line, and so can be shown
/// neither here nor on any other service. Zero once every invoice a client holds was raised
/// with a service in hand.
/// </param>
public sealed record ServiceInvoicesDto(
    IReadOnlyList<InvoiceDto> Invoices,
    int UnattributedInvoiceCount);
