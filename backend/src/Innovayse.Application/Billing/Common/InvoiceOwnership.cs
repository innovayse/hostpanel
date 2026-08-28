namespace Innovayse.Application.Billing.Common;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Resolves invoice ownership against the client the current credential names.
/// </summary>
/// <param name="invoices">Invoice repository.</param>
/// <param name="clients">Client repository, for mapping the caller's subject to their account.</param>
/// <param name="caller">Who is asking. Nothing tells this type whose invoices to consider.</param>
public sealed class InvoiceOwnership(
    IInvoiceRepository invoices,
    IClientRepository clients,
    ICurrentRequestContext caller) : IInvoiceOwnership
{
    /// <inheritdoc/>
    public async Task RequireOwnedByCallerAsync(int invoiceId, CancellationToken ct)
    {
        var userId = caller.RequireUserId();

        var client = await clients.FindByUserIdAsync(userId, ct);
        if (client is not null)
        {
            var invoice = await invoices.FindByIdAsync(invoiceId, ct);
            if (invoice is not null && invoice.ClientId == client.Id)
            {
                return;
            }
        }

        // An invoice that does not exist, an invoice belonging to somebody else, and a caller with
        // no client record all land here and answer identically. Distinguishing them would turn
        // this route into a way of enumerating ids -- and invoice ids are sequential integers.
        throw new InvoiceNotFoundException(invoiceId);
    }
}
