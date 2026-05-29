namespace Innovayse.Application.Support.Queries.GetClientTicketStats;

/// <summary>Query to retrieve ticket statistics for a specific client.</summary>
/// <param name="ClientId">FK to the client.</param>
public record GetClientTicketStatsQuery(int ClientId);
