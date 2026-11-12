using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Products.Commands.CreateProductBundle;

/// <summary>Command to create a product bundle.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="SortOrder">Position in the list, lowest first.</param>
/// <param name="IncludesDomain">Whether a domain registration is included.</param>
/// <param name="DomainYears">Years of registration included, 1 to 10; null when no domain is included.</param>
/// <param name="DiscountType">How the bundle is discounted.</param>
/// <param name="DiscountAmount">A percentage from 0 to 100, a fixed amount, or zero for no discount.</param>
/// <param name="DiscountCurrency">The ISO 4217 code of a fixed discount; null for any other type.</param>
/// <param name="Items">The components: at least one product (never a domain product), none twice, exactly one primary.</param>
public sealed record CreateProductBundleCommand(
    string Name,
    string? Description,
    int SortOrder,
    bool IncludesDomain,
    int? DomainYears,
    BundleDiscountType DiscountType,
    decimal DiscountAmount,
    string? DiscountCurrency,
    IReadOnlyList<ProductBundleItemInput> Items);
