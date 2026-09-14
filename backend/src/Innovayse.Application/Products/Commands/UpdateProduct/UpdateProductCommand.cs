namespace Innovayse.Application.Products.Commands.UpdateProduct;

using Innovayse.Application.Products.Common;

/// <summary>Command to update an existing product's details and prices.</summary>
/// <param name="Id">Product primary key.</param>
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
public record UpdateProductCommand(
    int Id,
    string Name,
    string? Description,
    string? Website,
    string? Slug,
    string? PackageName,
    IReadOnlyList<ProductPriceInput>? Prices,
    int? ServerGroupId,
    decimal? MonthlyPrice = null,
    decimal? AnnualPrice = null);
