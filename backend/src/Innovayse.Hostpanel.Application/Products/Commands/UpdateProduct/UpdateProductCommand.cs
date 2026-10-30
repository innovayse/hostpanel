using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Domain.Products;

namespace Innovayse.Hostpanel.Application.Products.Commands.UpdateProduct;

/// <summary>Command to update an existing product's details and prices.</summary>
/// <param name="Id">
/// Product primary key; wins over any id in the body. A value type, so it is left non-nullable:
/// unlike a bound string, a missing <c>int</c> in the body simply binds to <c>0</c> rather than
/// being treated as a required member, and the route overlay (<c>command with { Id = id }</c>)
/// replaces it before the handler ever sees that default.
/// </param>
/// <param name="Name">New display name.</param>
/// <param name="Description">New description.</param>
/// <param name="Website">Website URL for the product's landing page, or null to clear.</param>
/// <param name="Slug">URL-friendly slug, or null to clear.</param>
/// <param name="PackageName">Hosting package name, or null to clear.</param>
/// <param name="Prices">
/// The product's prices after the update, one entry per currency it sells in. The list is the
/// whole truth: a currency left out is removed, and an empty list leaves the product unsellable.
/// When absent the two legacy fields supply one entry in the base currency.
/// </param>
/// <param name="ServerGroupId">Optional FK to the server group, or null to clear.</param>
/// <param name="MonthlyPrice">
/// Legacy single-currency monthly price, sent by the pre-multi-currency admin form. Exists for
/// one release; read only when <paramref name="Prices"/> is absent, as the base-currency price.
/// </param>
/// <param name="AnnualPrice">
/// Legacy single-currency annual price, sent by the pre-multi-currency admin form. Exists for
/// one release; read only when <paramref name="Prices"/> is absent, as the base-currency price.
/// </param>
/// <param name="IsFeatured">
/// Whether this is the group's recommended plan; null keeps the stored value. When true the
/// handler clears the flag on the group's other products, so at most one stays featured.
/// </param>
/// <param name="SortOrder">Position inside the group, lowest first; null keeps the stored value.</param>
/// <param name="Sku">
/// The stock-keeping unit a fulfilment plugin keys on, in any case; stored normalised and unique
/// across products. Null keeps the stored SKU and an empty string clears it, so a form that does not
/// know the field yet never wipes it.
/// </param>
/// <param name="Metadata">
/// A JSON object of at most <see cref="Product.MaxMetadataBytes"/> bytes telling a fulfilment plugin
/// what to deliver. Null keeps the stored bag and an empty string clears it.
/// </param>
/// <param name="GracePeriodDays">
/// Days a service stays usable after its due date before it is suspended, from
/// <see cref="Product.MinGracePeriodDays"/> to <see cref="Product.MaxGracePeriodDays"/>; null keeps
/// the stored value.
/// </param>
public sealed record UpdateProductCommand(
    int Id,
    string Name,
    string? Description,
    string? Website,
    string? Slug,
    string? PackageName,
    IReadOnlyList<ProductPriceDto>? Prices,
    int? ServerGroupId,
    decimal? MonthlyPrice = null,
    decimal? AnnualPrice = null,
    bool? IsFeatured = null,
    int? SortOrder = null,
    string? Sku = null,
    string? Metadata = null,
    int? GracePeriodDays = null);
