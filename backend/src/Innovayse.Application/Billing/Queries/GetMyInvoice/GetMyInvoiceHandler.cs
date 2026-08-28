namespace Innovayse.Application.Billing.Queries.GetMyInvoice;

using Innovayse.Application.Billing.Common;
using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Queries.GetInvoice;
using Wolverine;

/// <summary>
/// Returns one of the calling client's own invoices, refusing every id that is not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// read an invoice through <see cref="GetMyInvoiceQuery"/> without it having run. Once ownership
/// is settled the projection is the same read the admin route performs, so this dispatches
/// <see cref="GetInvoiceQuery"/> rather than growing a second copy of the mapping.
/// </remarks>
/// <param name="ownership">The rule that says a client may only read their own invoices.</param>
/// <param name="bus">Wolverine bus, used to reach the shared read once ownership is settled.</param>
public sealed class GetMyInvoiceHandler(IInvoiceOwnership ownership, IMessageBus bus)
{
    /// <summary>Handles <see cref="GetMyInvoiceQuery"/>.</summary>
    /// <param name="query">The query. It names no account: this reads the caller's own invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="InvoiceDto"/>.</returns>
    /// <exception cref="InvoiceNotFoundException">
    /// Thrown when the invoice is not the caller's, when no such invoice exists, and when the
    /// caller has no client record -- all with the same wording and status.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<InvoiceDto> HandleAsync(GetMyInvoiceQuery query, CancellationToken ct)
    {
        await ownership.RequireOwnedByCallerAsync(query.InvoiceId, ct);
        return await bus.InvokeAsync<InvoiceDto>(new GetInvoiceQuery(query.InvoiceId), ct);
    }
}
