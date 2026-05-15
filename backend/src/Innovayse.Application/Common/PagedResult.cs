namespace Innovayse.Application.Common;

/// <summary>
/// Wraps a paginated result set returned from list queries.
/// </summary>
/// <typeparam name="T">The type of items in the result.</typeparam>
/// <param name="Items">The items for the current page.</param>
/// <param name="TotalCount">Total number of matching items across all pages.</param>
/// <param name="Page">The current 1-based page number.</param>
/// <param name="PageSize">The number of items per page.</param>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
