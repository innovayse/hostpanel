namespace Innovayse.Application.Billing.Common;

/// <summary>
/// Enforces that an invoice belongs to the calling client.
/// </summary>
/// <remarks>
/// <para>
/// The client-facing billing routes used to carry the rule as
/// <c>if (invoice.ClientId != profile.Id) return Forbid();</c> written out four times in
/// <c>MyBillingController</c>. It worked, but it lived at the endpoint: every new action had to
/// remember it, and it travelled separately from the command, so anything that dispatched
/// <c>PayInvoiceCommand</c> or <c>StartGatewayPaymentCommand</c> without going through that
/// controller was unchecked.
/// </para>
/// <para>
/// It now sits in the Application layer beside <c>ITicketOwnership</c> and
/// <c>IDomainOwnership</c>, resolving the caller itself through <c>ICurrentRequestContext</c>
/// rather than being told whom to check, and is called by the client-facing handlers rather than
/// by the endpoint.
/// </para>
/// </remarks>
public interface IInvoiceOwnership
{
    /// <summary>
    /// Verifies that the invoice belongs to the client the current credential names, and refuses
    /// the request when it does not.
    /// </summary>
    /// <param name="invoiceId">Invoice primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when, and only when, the caller owns the invoice.</returns>
    /// <exception cref="InvoiceNotFoundException">
    /// Thrown when the invoice belongs to somebody else, when no such invoice exists, and when
    /// the caller has no client record. The three answer alike on purpose: telling them apart
    /// would let the route be used to enumerate invoice ids.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    Task RequireOwnedByCallerAsync(int invoiceId, CancellationToken ct);
}
