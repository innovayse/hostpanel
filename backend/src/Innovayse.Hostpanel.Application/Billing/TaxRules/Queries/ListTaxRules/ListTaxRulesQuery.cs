using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Billing.TaxRules.Queries.ListTaxRules;

/// <summary>Query for a page of tax rules, with optional filters.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">Optional text; only rules whose name, country code or region contains it (case-insensitive) are returned. Blank returns all.</param>
/// <param name="Enabled">Optional switch: true keeps the rules new invoices use, false the ones switched off; null returns both.</param>
public sealed record ListTaxRulesQuery(
    int Page,
    int PageSize,
    string? Search = null,
    bool? Enabled = null) : IListQuery;
