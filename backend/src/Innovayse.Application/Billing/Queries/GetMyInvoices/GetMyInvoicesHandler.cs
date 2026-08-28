namespace Innovayse.Application.Billing.Queries.GetMyInvoices;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Queries.GetInvoice;
using Innovayse.Application.Clients.Common;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>Returns the calling client's invoices, newest first.</summary>
/// <param name="repo">Invoice repository.</param>
/// <param name="clientRepo">Resolves the caller's client record.</param>
/// <param name="caller">Who is asking; the query does not say, and must not.</param>
public sealed class GetMyInvoicesHandler(
    IInvoiceRepository repo,
    IClientRepository clientRepo,
    ICurrentRequestContext caller)
{
    /// <summary>
    /// Handles <see cref="GetMyInvoicesQuery"/>.
    /// </summary>
    /// <param name="query">The query. It names no account: this reads the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All invoices for the client with their line items and transactions, ordered newest first.</returns>
    /// <exception cref="ClientProfileNotFoundException">Thrown when the caller has no client record.</exception>
    public async Task<IReadOnlyList<InvoiceDto>> HandleAsync(GetMyInvoicesQuery query, CancellationToken ct)
    {
        var userId = caller.RequireUserId();
        var client = await clientRepo.FindByUserIdAsync(userId, ct)
            ?? throw new ClientProfileNotFoundException(userId);

        var invoices = await repo.ListByClientAsync(client.Id, ct);

        return invoices.Select(inv => GetInvoiceHandler.MapToDto(inv)).ToList();
    }
}
