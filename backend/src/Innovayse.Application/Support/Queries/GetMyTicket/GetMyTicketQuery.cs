namespace Innovayse.Application.Support.Queries.GetMyTicket;

/// <summary>Query to retrieve one of the calling client's own support tickets.</summary>
/// <remarks>
/// Carries a ticket id but no client id. Which account the ticket must belong to is resolved
/// inside the handler from the credential, so the scoping cannot be separated from the message
/// the way an id in the body can. The admin read that may return any ticket is a separate use
/// case, <c>GetTicketQuery</c>.
/// </remarks>
/// <param name="Id">The ticket primary key.</param>
public record GetMyTicketQuery(int Id);
