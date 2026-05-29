namespace Innovayse.Application.Billing.Queries.ListBillableItems;

<<<<<<< HEAD
/// <summary>Query to fetch a paginated list of billable items with optional type filter.</summary>
/// <param name="Page">1-based page number (default 1).</param>
/// <param name="PageSize">Items per page (default 20, max 100).</param>
/// <param name="Type">Optional filter by item type (OneTime, Recurring, or null for all).</param>
public sealed record ListBillableItemsQuery(int Page = 1, int PageSize = 20, string? Type = null);
=======
/// <summary>Query to list billable items (uninvoiced + paginated invoiced) for a client.</summary>
/// <param name="ClientId">The client's primary key.</param>
/// <param name="InvoicedPage">1-based page number for invoiced items (default 1).</param>
/// <param name="InvoicedPageSize">Page size for invoiced items (default 20).</param>
public record ListBillableItemsQuery(int ClientId, int InvoicedPage = 1, int InvoicedPageSize = 20);
>>>>>>> origin/main
