using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Application.Fraud.Queries.ListBlockListEntries;

/// <summary>Lists the block list, newest first, optionally searched and for one type.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">Optional text; only entries whose value or note contains it (case-insensitive) are listed. Blank lists all.</param>
/// <param name="Type">Optional filter by what the value names; an unknown value is refused, never ignored.</param>
public sealed record ListBlockListEntriesQuery(
    int Page,
    int PageSize,
    string? Search = null,
    BlockListType? Type = null) : IListQuery;
