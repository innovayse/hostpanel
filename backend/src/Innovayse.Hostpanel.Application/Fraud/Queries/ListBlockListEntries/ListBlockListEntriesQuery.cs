using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Application.Fraud.Queries.ListBlockListEntries;

/// <summary>Lists the block list, newest first.</summary>
/// <param name="Page">One-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
/// <param name="Type">Optional filter by what the value names; an unknown value is refused when the query is bound, never ignored.</param>
public sealed record ListBlockListEntriesQuery(int Page, int PageSize, BlockListType? Type);
