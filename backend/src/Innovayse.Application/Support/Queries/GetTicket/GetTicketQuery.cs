namespace Innovayse.Application.Support.Queries.GetTicket;

/// <summary>Query to retrieve a single support ticket with all its replies.</summary>
/// <param name="Id">The ticket primary key.</param>
public record GetTicketQuery(int Id);
