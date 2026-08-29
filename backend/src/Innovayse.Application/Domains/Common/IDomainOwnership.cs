namespace Innovayse.Application.Domains.Common;

/// <summary>
/// Enforces that a domain belongs to the calling client.
/// </summary>
/// <remarks>
/// <para>
/// The client-facing domain routes may only ever touch the caller's own domains, and the rule
/// that decides that used to run in <c>MyDomainsController</c> -- eighteen times, each one an
/// <c>if (!await ownership.IsOwnedByCallerAsync(id, ct)) return Forbid();</c> ahead of the
/// dispatch. That was better than reading claims in the controller, but the check still
/// travelled separately from the message: every one of the commands behind those actions is
/// also dispatched by the admin <c>DomainsController</c>, so the guarantee held only for
/// callers who happened to come through that one endpoint.
/// </para>
/// <para>
/// It now sits beside <c>ITicketOwnership</c> and <c>IInvoiceOwnership</c> and is shaped like
/// them: it resolves the caller through <c>ICurrentRequestContext</c> rather than being told
/// whom to check, and it refuses rather than answering a <see langword="bool"/>. Every
/// client-facing domain use case wants the same refusal, and putting the throw here means it is
/// worded in one place and a new handler cannot ask the question and then forget to act on the
/// answer. The eighteen <c>My*</c> handlers call it; the endpoint no longer does.
/// </para>
/// </remarks>
public interface IDomainOwnership
{
    /// <summary>
    /// Verifies that the domain belongs to the client the current credential names, and refuses
    /// the request when it does not.
    /// </summary>
    /// <param name="domainId">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when, and only when, the caller owns the domain.</returns>
    /// <exception cref="DomainNotFoundException">
    /// Thrown when the domain belongs to somebody else, when no such domain exists, and when the
    /// caller has no client record. The three answer alike on purpose: telling them apart would
    /// let the route be used to enumerate domain ids.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    Task RequireOwnedByCallerAsync(int domainId, CancellationToken ct);
}
