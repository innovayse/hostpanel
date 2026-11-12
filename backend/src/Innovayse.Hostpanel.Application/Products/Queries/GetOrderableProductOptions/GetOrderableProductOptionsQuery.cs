namespace Innovayse.Hostpanel.Application.Products.Queries.GetOrderableProductOptions;

/// <summary>Requests the option groups a client is offered when ordering one active product.</summary>
/// <param name="ProductId">Product being ordered.</param>
public sealed record GetOrderableProductOptionsQuery(int ProductId);
