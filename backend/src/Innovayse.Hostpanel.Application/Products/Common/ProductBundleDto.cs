using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Products.Common;

/// <summary>A product bundle as staff edit it, with every field and its components.</summary>
/// <param name="Id">Primary key.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="IsActive">Whether the bundle is visible in the storefront.</param>
/// <param name="SortOrder">Position in the list, lowest first.</param>
/// <param name="IncludesDomain">Whether a domain registration is included.</param>
/// <param name="DomainYears">Years of registration included; null when no domain is included.</param>
/// <param name="DiscountType">How the bundle is discounted.</param>
/// <param name="DiscountAmount">The discount: a percentage or a fixed amount, zero for none.</param>
/// <param name="DiscountCurrency">The currency of a fixed discount; null otherwise.</param>
/// <param name="Components">The products the bundle is made of, the primary one first.</param>
public sealed record ProductBundleDto(
    int Id,
    string Name,
    string? Description,
    bool IsActive,
    int SortOrder,
    bool IncludesDomain,
    int? DomainYears,
    BundleDiscountType DiscountType,
    decimal DiscountAmount,
    string? DiscountCurrency,
    IReadOnlyList<ProductBundleComponentDto> Components);
