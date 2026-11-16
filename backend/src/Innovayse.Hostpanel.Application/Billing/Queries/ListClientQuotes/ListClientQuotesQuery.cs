using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListClientQuotes;

/// <summary>Paginated, optionally searched and filtered query for a specific client's quotes.</summary>
/// <param name="ClientId">The client's primary key; overlaid from the route by the controller.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">
/// Optional text; only quotes whose subject contains it (case-insensitive), or whose id it is when it is a whole number,
/// are listed. Blank lists all.
/// </param>
/// <param name="Stage">Optional stage filter, bound by name from the query string; <see langword="null"/> for every stage.</param>
/// <param name="Validity">Optional validity filter, bound by name from the query string; <see langword="null"/> for valid and expired quotes.</param>
public sealed record ListClientQuotesQuery(
    int ClientId,
    int Page,
    int PageSize,
    string? Search = null,
    QuoteStage? Stage = null,
    QuoteValidity? Validity = null) : IListQuery, IQuoteListFilters;
