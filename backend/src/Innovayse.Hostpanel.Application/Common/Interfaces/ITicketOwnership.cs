using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Enforces that a support ticket belongs to the client account the caller acts for.
/// </summary>
/// <remarks>
/// <para>
/// The client-facing ticket routes used to dispatch <c>GetTicketQuery</c> and
/// <c>ReplyToTicketCommand</c> with nothing but the route id and no ownership check of any kind,
/// so any authenticated client could read and reply to any other customer's ticket by walking
/// the sequential ids. This is the rule that was missing.
/// </para>
/// <para>
/// It sits beside <c>IDomainOwnership</c> and <c>IInvoiceOwnership</c> and is shaped like them: it
/// resolves the caller through <c>ICurrentUserService</c> rather than being told whom to check,
/// and it answers a <see cref="Result"/> carrying <c>TICKET_NOT_FOUND</c> on refusal instead of
/// throwing a typed exception, so every client-facing ticket handler can propagate it the same way
/// it propagates any other refusal.
/// </para>
/// </remarks>
public interface ITicketOwnership
{
    /// <summary>
    /// Verifies that the ticket belongs to the account the caller acts for, as owner or linked user.
    /// </summary>
    /// <param name="ticketId">Ticket primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success when the ticket is the caller's own, or a failed <see cref="Result"/> carrying
    /// <c>TICKET_NOT_FOUND</c> when the ticket belongs to somebody else, when no such ticket
    /// exists, or when the caller acts for no account. The three answer alike on purpose:
    /// telling them apart would let the route be used to enumerate ticket ids.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public Task<Result> RequireOwnedByCallerAsync(int ticketId, CancellationToken ct);
}
