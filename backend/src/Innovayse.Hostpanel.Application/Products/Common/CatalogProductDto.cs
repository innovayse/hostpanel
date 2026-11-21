using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Products.Common;

/// <summary>
/// One product as the storefront shows it to anyone, signed in or not. The provisioning package,
/// the server group and the status of <see cref="ProductDto"/> are not part of it: the catalogue
/// only ever lists active products, and how a product is provisioned is for staff only.
/// </summary>
/// <param name="Id">Primary key.</param>
/// <param name="GroupId">Parent product group ID.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Website">Optional website URL for the product's landing page.</param>
/// <param name="Slug">Optional URL-friendly slug.</param>
/// <param name="Type">Product type.</param>
/// <param name="Pricing">Monthly and annual pricing in the caller's currency.</param>
/// <param name="Prices">Every stored price, one entry per currency the product sells in.</param>
/// <param name="IsFeatured">Whether the product is the group's recommended plan.</param>
/// <param name="SortOrder">Operator-defined position inside the group, lowest first.</param>
/// <param name="Sku">The normalised stock-keeping unit that names the product, or null when none is set.</param>
public sealed record CatalogProductDto(
    int Id,
    int GroupId,
    string Name,
    string? Description,
    string? Website,
    string? Slug,
    ProductType Type,
    ProductPricingDto Pricing,
    IReadOnlyList<ProductPriceDto> Prices,
    bool IsFeatured,
    int SortOrder,
    string? Sku);
