namespace Innovayse.Application.Support.Queries.ListTickets;

/// <summary>Query to retrieve a paged list of all support tickets.</summary>
/// <param name="Page">One-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
public record ListTicketsQuery(int Page, int PageSize);
