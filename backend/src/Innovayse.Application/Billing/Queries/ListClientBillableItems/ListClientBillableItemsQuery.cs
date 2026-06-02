namespace Innovayse.Application.Billing.Queries.ListClientBillableItems;

/// <summary>Query to list billable items (uninvoiced + paginated invoiced) for a client.</summary>
/// <param name="ClientId">The client's primary key.</param>
/// <param name="InvoicedPage">1-based page number for invoiced items (default 1).</param>
/// <param name="InvoicedPageSize">Page size for invoiced items (default 20).</param>
public record ListClientBillableItemsQuery(int ClientId, int InvoicedPage = 1, int InvoicedPageSize = 20);
