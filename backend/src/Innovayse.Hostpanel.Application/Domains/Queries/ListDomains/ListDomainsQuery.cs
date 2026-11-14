using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains.Enums;

namespace Innovayse.Hostpanel.Application.Domains.Queries.ListDomains;

/// <summary>Query to retrieve a paginated list of all domains (admin view), with optional filters.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">Optional text; only domains whose name contains it (case-insensitive) are returned. Blank returns all.</param>
/// <param name="ClientId">Optional client ID to filter domains by owner; null returns all.</param>
/// <param name="Status">Optional lifecycle status; null returns every status.</param>
/// <param name="RegistrarModule">
/// Optional registrar module the domain was registered, transferred or imported with; null returns every registrar. A
/// domain recorded without a module (it predates the column) is left out when a registrar is chosen.
/// </param>
public sealed record ListDomainsQuery(
    int Page,
    int PageSize,
    string? Search = null,
    int? ClientId = null,
    DomainStatus? Status = null,
    RegistrarModule? RegistrarModule = null) : IListQuery;
