namespace Innovayse.Hostpanel.Application.Products.Common;

/// <summary>One product a bundle is made of, as staff and the storefront both read it.</summary>
/// <param name="ProductId">The product's primary key.</param>
/// <param name="Name">The product's display name.</param>
/// <param name="IsPrimary">Whether this is the bundle's primary component.</param>
public sealed record ProductBundleComponentDto(int ProductId, string Name, bool IsPrimary);
