using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Products.Common;

/// <summary>
/// Represents a product in the staff list. Carries the provisioning and fulfilment fields, so it is
/// never answered to the storefront (which reads <see cref="CatalogProductDto"/>).
/// </summary>
/// <param name="Id">Primary key.</param>
/// <param name="GroupId">Parent product group ID.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Website">Optional website URL for the product's landing page.</param>
/// <param name="Slug">Optional URL-friendly slug.</param>
/// <param name="PackageName">Optional hosting package name used for provisioning.</param>
/// <param name="Type">Product type.</param>
/// <param name="Status">Current status.</param>
/// <param name="Pricing">Monthly and annual pricing in the caller's currency.</param>
/// <param name="Prices">Every stored price, one entry per currency the product sells in.</param>
/// <param name="ServerGroupId">Optional FK to the server group for provisioning.</param>
/// <param name="IsFeatured">Whether the product is the group's recommended plan.</param>
/// <param name="SortOrder">Operator-defined position inside the group, lowest first.</param>
/// <param name="Sku">The normalised stock-keeping unit a fulfilment plugin keys on, or null when none is set.</param>
/// <param name="Metadata">The metadata bag as JSON object text, or null when none is set.</param>
/// <param name="GracePeriodDays">Days a service stays usable after its due date before it is suspended.</param>
public sealed record ProductDto(
    int Id,
    int GroupId,
    string Name,
    string? Description,
    string? Website,
    string? Slug,
    string? PackageName,
    ProductType Type,
    ProductStatus Status,
    ProductPricingDto Pricing,
    IReadOnlyList<ProductPriceDto> Prices,
    int? ServerGroupId,
    bool IsFeatured,
    int SortOrder,
    string? Sku,
    string? Metadata,
    int GracePeriodDays);
