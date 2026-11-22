namespace Innovayse.Hostpanel.Application.Products.Queries.GetAllowedPaymentMethods;

/// <summary>
/// Reads the payment methods the product groups of some products allow together. The billing module asks it
/// so it never reads the Products tables.
/// </summary>
/// <param name="ProductIds">The products; an id that names no product is ignored.</param>
public sealed record GetAllowedPaymentMethodsQuery(IReadOnlyList<int> ProductIds);
