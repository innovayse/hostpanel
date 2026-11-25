namespace Innovayse.Hostpanel.Application.Products.Common;

/// <summary>
/// The product a domain order line is placed against. It carries the id only: the order line needs nothing else,
/// and the anonymous storefront route that answers it must not publish a staff-written product name.
/// </summary>
/// <param name="Id">The product id the order line carries.</param>
public sealed record DomainProductDto(int Id);
