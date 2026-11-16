using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains.Enums;

namespace Innovayse.Hostpanel.Application.Domains.Queries.ListTldConfigs;

/// <summary>Query to retrieve a paginated list of TLD configurations for the admin list view, with optional filters.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">Optional text; only configurations whose TLD contains it (case-insensitive) are returned. Blank returns all.</param>
/// <param name="RegistrarModule">Optional registrar module the TLD is sourced from; null returns every registrar.</param>
/// <param name="Enabled">Optional switch: true keeps the TLDs on sale, false the ones switched off; null returns both.</param>
public sealed record ListTldConfigsQuery(
    int Page,
    int PageSize,
    string? Search = null,
    RegistrarModule? RegistrarModule = null,
    bool? Enabled = null) : IListQuery;
