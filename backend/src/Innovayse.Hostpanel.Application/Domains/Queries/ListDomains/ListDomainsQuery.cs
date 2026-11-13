namespace Innovayse.Hostpanel.Application.Domains.Queries.ListDomains;

/// <summary>Query to retrieve a paginated list of all domains (admin view).</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
/// <param name="ClientId">Optional client ID to filter domains by owner; null returns all.</param>
/// <param name="Search">Optional text; only domains whose name contains it (case-insensitive) are returned. Blank returns all.</param>
public sealed record ListDomainsQuery(int Page, int PageSize, int? ClientId = null, string? Search = null);
