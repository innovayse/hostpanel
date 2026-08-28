namespace Innovayse.Application.Domains.Common;

/// <summary>
/// Answers whether the calling client owns a domain.
/// </summary>
/// <remarks>
/// <para>
/// The client-facing domain routes may only ever touch the caller's own domains, and the rule
/// that decides that used to be written out inside the controller — a private helper that read
/// the claims itself and ran before the command was dispatched. Two things were wrong with
/// that: the rule was unreachable from anywhere else, so every new action had to remember to
/// call it, and it travelled separately from the message, so anything that dispatched the same
/// command without going through that endpoint was unchecked.
/// </para>
/// <para>
/// Here it is one Application-layer type, resolving the caller through
/// <c>ICurrentRequestContext</c> rather than being told who to check. A handler can call it as
/// readily as an endpoint can.
/// </para>
/// </remarks>
public interface IDomainOwnership
{
    /// <summary>
    /// Determines whether the domain belongs to the client the current credential names.
    /// </summary>
    /// <param name="domainId">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the caller owns it; <see langword="false"/> when they do not,
    /// when no such domain exists, and when the caller has no client record. The three answer
    /// alike on purpose: telling them apart would let the route be used to find out which
    /// domain ids exist.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    Task<bool> IsOwnedByCallerAsync(int domainId, CancellationToken ct);
}
