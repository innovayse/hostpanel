using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Products.Commands.UpdateProductBundle;

/// <summary>Command to replace a product bundle's details and components.</summary>
/// <param name="Id">
/// Product bundle primary key; wins over any id in the body. A value type, so it is left
/// non-nullable: a missing <c>int</c> in the body binds to <c>0</c> rather than being treated as
/// a required member, and the route overlay (<c>command with { Id = id }</c>) replaces it.
/// </param>
/// <param name="Name">New display name.</param>
/// <param name="Description">New description.</param>
/// <param name="SortOrder">New position in the list, lowest first.</param>
/// <param name="IncludesDomain">Whether a domain registration is included.</param>
/// <param name="DomainYears">Years of registration included, 1 to 10; null when no domain is included.</param>
/// <param name="DiscountType">How the bundle is discounted.</param>
/// <param name="DiscountAmount">A percentage from 0 to 100, a fixed amount, or zero for no discount.</param>
/// <param name="DiscountCurrency">The ISO 4217 code of a fixed discount; null for any other type.</param>
/// <param name="Items">The new components: at least one product (never a domain product), none twice, exactly one primary.</param>
/// <param name="IsActive">Whether the bundle is visible in the storefront; a body that leaves it out keeps the bundle as it is.</param>
public sealed record UpdateProductBundleCommand(
    int Id,
    string Name,
    string? Description,
    int SortOrder,
    bool IncludesDomain,
    int? DomainYears,
    BundleDiscountType DiscountType,
    decimal DiscountAmount,
    string? DiscountCurrency,
    IReadOnlyList<ProductBundleItemInput> Items,
    bool? IsActive = null);
