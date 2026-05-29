namespace Innovayse.Application.Orders.Queries.GetOrder;

/// <summary>Query to retrieve a single order with all its items.</summary>
/// <param name="OrderId">The order primary key.</param>
public record GetOrderQuery(int OrderId);
