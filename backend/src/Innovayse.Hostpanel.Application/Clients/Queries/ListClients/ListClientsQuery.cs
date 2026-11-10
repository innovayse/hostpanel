using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Application.Clients.Queries.ListClients;

/// <summary>
/// Query to retrieve a paginated list of clients, with optional filters.
/// </summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">
/// Optional search term, matched case-insensitively against the first name, last name, full name
/// ("First Last"), company and, when the term is a whole number, the client id.
/// </param>
/// <param name="Email">Optional sign-in email filter (partial match).</param>
/// <param name="Phone">Optional phone filter (partial match).</param>
/// <param name="Status">Optional status filter.</param>
/// <param name="ClientGroupId">Optional client group filter: only the clients that belong to this group.</param>
public sealed record ListClientsQuery(
    int Page,
    int PageSize,
    string? Search = null,
    string? Email = null,
    string? Phone = null,
    ClientStatus? Status = null,
    int? ClientGroupId = null);
