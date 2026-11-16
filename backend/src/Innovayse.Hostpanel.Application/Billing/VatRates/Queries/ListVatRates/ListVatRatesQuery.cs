using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Billing.VatRates.Queries.ListVatRates;

/// <summary>Query for a page of VAT rates, with an optional country search.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">Optional text; only rows whose country code contains it (case-insensitive) are returned. Blank returns all.</param>
public sealed record ListVatRatesQuery(int Page, int PageSize, string? Search = null) : IListQuery;
