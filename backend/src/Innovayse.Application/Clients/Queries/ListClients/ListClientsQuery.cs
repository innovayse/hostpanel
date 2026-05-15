namespace Innovayse.Application.Clients.Queries.ListClients;

/// <summary>
/// Query to retrieve a paginated list of clients, with optional filters.
/// </summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1–100).</param>
/// <param name="Search">Optional search term matched against name, company.</param>
/// <param name="Email">Optional email filter (partial match).</param>
/// <param name="Phone">Optional phone filter (partial match).</param>
/// <param name="Status">Optional status filter (Active, Inactive, Suspended, Closed).</param>
public record ListClientsQuery(
    int Page,
    int PageSize,
    string? Search = null,
    string? Email = null,
    string? Phone = null,
    string? Status = null);
