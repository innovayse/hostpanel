namespace Innovayse.Application.Support.Common;

/// <summary>
/// Enforces that a support ticket belongs to the calling client.
/// </summary>
/// <remarks>
/// <para>
/// The client-facing ticket routes used to dispatch <c>GetTicketQuery</c> and
/// <c>ReplyToTicketCommand</c> with nothing but the route id and no ownership check of any kind,
/// so any authenticated client could read and reply to any other customer's ticket by walking
/// the sequential ids. This is the rule that was missing.
/// </para>
/// <para>
/// It follows <c>IDomainOwnership</c>: subject to client to resource, resolving the caller itself
/// through <c>ICurrentRequestContext</c> rather than being told whom to check, so a handler can
/// call it as readily as an endpoint can. It differs from that type in refusing rather than
/// answering a <see langword="bool"/>. Every client-facing ticket use case wants the same
/// refusal, and putting the throw here means it is worded in one place and a new handler cannot
/// ask the question and then forget to act on the answer.
/// </para>
/// </remarks>
public interface ITicketOwnership
{
    /// <summary>
    /// Verifies that the ticket belongs to the client the current credential names, and refuses
    /// the request when it does not.
    /// </summary>
    /// <param name="ticketId">Ticket primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when, and only when, the caller owns the ticket.</returns>
    /// <exception cref="TicketNotFoundException">
    /// Thrown when the ticket belongs to somebody else, when no such ticket exists, and when the
    /// caller has no client record. The three answer alike on purpose: telling them apart would
    /// let the route be used to enumerate ticket ids.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    Task RequireOwnedByCallerAsync(int ticketId, CancellationToken ct);
}
