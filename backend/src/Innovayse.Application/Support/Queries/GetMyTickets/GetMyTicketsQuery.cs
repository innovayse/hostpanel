namespace Innovayse.Application.Support.Queries.GetMyTickets;

/// <summary>Query to retrieve all tickets belonging to a specific client.</summary>
/// <param name="ClientId">FK to the client.</param>
public record GetMyTicketsQuery(int ClientId);
