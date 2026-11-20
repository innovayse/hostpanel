using Innovayse.Hostpanel.Application.Billing.VatRates.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.VatRates.Queries.ListVatRates;

/// <summary>Query for a page of VAT rates, with an optional country search and kind and status filters.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">Optional text; only rows whose country code contains it (case-insensitive) are returned. Blank returns all.</param>
/// <param name="Kind">Optional kind of rate; null returns the standard and the reduced rows.</param>
/// <param name="Status">Optional standing of the row today (in force, superseded, upcoming); null returns every row.</param>
public sealed record ListVatRatesQuery(
    int Page,
    int PageSize,
    string? Search = null,
    VatRateKind? Kind = null,
    VatRateStatus? Status = null) : IListQuery;
