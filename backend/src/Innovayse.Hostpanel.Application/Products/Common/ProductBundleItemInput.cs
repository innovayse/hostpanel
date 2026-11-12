namespace Innovayse.Hostpanel.Application.Products.Common;

/// <summary>One component an admin puts into a product bundle.</summary>
/// <param name="ProductId">The catalogue product sold as this component; never a domain product.</param>
/// <param name="IsPrimary">Whether this is the bundle's primary component; exactly one component is.</param>
public sealed record ProductBundleItemInput(int ProductId, bool IsPrimary);
