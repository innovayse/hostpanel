namespace Innovayse.Application.Billing.Queries.GetMyServiceInvoices;

using Innovayse.Application.Billing.Queries.GetInvoice;
using Innovayse.Application.Services.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Returns the invoices charged to one of the calling client's own services, refusing every
/// service that is not theirs.
/// </summary>
/// <remarks>
/// <para>
/// The check lives here rather than at the endpoint, so it travels with the message. That
/// matters more than usual for this query: it reads financial records, and the id it is given
/// comes straight off a route whose sibling actions take the same kind of id and check nothing.
/// </para>
/// <para>
/// Ownership is settled twice over, deliberately. The service must belong to the caller, and the
/// invoice read is <i>also</i> scoped to that caller's client id rather than fetched by service
/// alone — so a service id that somehow passed the first check still cannot return another
/// account's invoices. Neither condition is load-bearing on its own being enough.
/// </para>
/// </remarks>
/// <param name="ownership">The rule that says a client may only look at their own services.</param>
/// <param name="invoices">Invoice repository.</param>
public sealed class GetMyServiceInvoicesHandler(
    IServiceOwnership ownership,
    IInvoiceRepository invoices)
{
    /// <summary>Handles <see cref="GetMyServiceInvoicesQuery"/>.</summary>
    /// <param name="query">The query. It names no account: this reads the caller's own service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The invoices recorded against the service, and how many of the caller's invoices are
    /// recorded against no service at all.
    /// </returns>
    /// <exception cref="MyServiceNotFoundException">
    /// Thrown when the service is not the caller's, when no such service exists, and when the
    /// caller has no client record -- all with the same wording and status.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<ServiceInvoicesDto> HandleAsync(GetMyServiceInvoicesQuery query, CancellationToken ct)
    {
        await ownership.RequireOwnedByCallerAsync(query.ServiceId, ct);

        var clientId = await ownership.RequireCallerClientIdAsync(ct);

        var linked = await invoices.ListByClientServiceAsync(clientId, query.ServiceId, ct);
        var unattributed = await invoices.CountUnattributedByClientAsync(clientId, ct);

        return new ServiceInvoicesDto(
            linked.Select(inv => GetInvoiceHandler.MapToDto(inv)).ToList(),
            unattributed);
    }
}
