namespace Innovayse.Application.Orders.Queries.ListOrders;

/// <summary>Query to retrieve a paged list of orders with optional status filtering.</summary>
/// <param name="Page">One-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
/// <param name="Status">Optional status filter string (parsed to <see cref="Domain.Orders.OrderStatus"/>).</param>
public record ListOrdersQuery(int Page, int PageSize, string? Status);
