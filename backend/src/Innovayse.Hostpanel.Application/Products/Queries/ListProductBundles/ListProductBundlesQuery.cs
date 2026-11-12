using Innovayse.Hostpanel.Application.Products.Common;

namespace Innovayse.Hostpanel.Application.Products.Queries.ListProductBundles;

/// <summary>
/// Returns every product bundle for staff, the ones switched off included, as <see cref="ProductBundleDto"/>
/// rows; the storefront reads <c>ListCatalogProductBundlesQuery</c>.
/// </summary>
public sealed record ListProductBundlesQuery;
