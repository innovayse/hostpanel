using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Enforces that a domain belongs to the calling client.
/// </summary>
/// <remarks>
/// <para>
/// The client-facing domain routes may only ever touch the caller's own domains. The check runs
/// in the client-facing <c>My*</c> handlers rather than at the endpoint, because the commands
/// behind those actions are also dispatched by the admin <c>DomainsController</c>.
/// </para>
/// <para>
/// It sits beside <c>ITicketOwnership</c> and <c>IInvoiceOwnership</c> and is shaped like them:
/// it resolves the caller through <c>ICurrentUserService</c> rather than being told whom to check,
/// and answers a <see cref="Result"/> carrying <c>DOMAIN_NOT_FOUND</c> on refusal, so every
/// client-facing domain handler propagates it the same way it propagates any other refusal.
/// </para>
/// </remarks>
public interface IDomainOwnership
{
    /// <summary>
    /// Verifies that the domain belongs to the client the current credential names.
    /// </summary>
    /// <param name="domainId">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success when the domain is the caller's own, or a failed <see cref="Result"/> carrying
    /// <c>DOMAIN_NOT_FOUND</c> when the domain belongs to somebody else, when no such domain
    /// exists, or when the caller has no client record. The three answer alike on purpose:
    /// telling them apart would let the route be used to enumerate domain ids.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public Task<Result> RequireOwnedByCallerAsync(int domainId, CancellationToken ct);
}
