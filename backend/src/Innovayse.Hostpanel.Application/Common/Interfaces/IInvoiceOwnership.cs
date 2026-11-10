using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Enforces that an invoice belongs to the calling client.
/// </summary>
/// <remarks>
/// <para>
/// The check travels with the command, not the endpoint: it is called by the client-facing
/// handlers (<c>StartMyGatewayPaymentCommand</c> and the rest), so no
/// dispatch path can skip it.
/// </para>
/// <para>
/// It sits in the Application layer beside <c>ITicketOwnership</c> and <c>IDomainOwnership</c>,
/// resolving the caller itself through <c>ICurrentUserService</c> rather than being told whom to
/// check.
/// </para>
/// <para>
/// The refusal is a <see cref="Result"/> the caller must inspect, matching
/// <c>IServiceOwnership</c>: a refusal nobody reads must not become silent, so every caller checks
/// <see cref="Result.IsFailure"/> and forwards <see cref="Result.ErrorCode"/> rather than letting
/// the call simply return.
/// </para>
/// </remarks>
public interface IInvoiceOwnership
{
    /// <summary>
    /// Verifies that the invoice belongs to the client the current credential names.
    /// </summary>
    /// <param name="invoiceId">Invoice primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success when the caller owns the invoice; otherwise a failure carrying
    /// <c>INVOICE_NOT_FOUND</c> (404). The invoice belonging to somebody else, no such invoice
    /// existing, and the caller having no client record all answer alike on purpose: telling them
    /// apart would let the route be used to enumerate invoice ids.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public Task<Result> RequireOwnedByCallerAsync(int invoiceId, CancellationToken ct);
}
