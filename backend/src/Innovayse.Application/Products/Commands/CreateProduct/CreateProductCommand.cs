namespace Innovayse.Application.Products.Commands.CreateProduct;

using Innovayse.Application.Products.Common;
using Innovayse.Domain.Products;

/// <summary>Command to create a new product in a product group.</summary>
/// <param name="GroupId">FK to the parent product group.</param>
/// <param name="Name">Product display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Website">Optional website URL for the product's landing page.</param>
/// <param name="Slug">Optional URL-friendly slug for the product.</param>
/// <param name="PackageName">Optional hosting package name used for provisioning.</param>
/// <param name="Type">Product type.</param>
/// <param name="Prices">The product's prices, one entry per currency it sells in; at least one.</param>
/// <param name="ServerGroupId">Optional FK to the server group for provisioning.</param>
public record CreateProductCommand(
    int GroupId,
    string Name,
    string? Description,
    string? Website,
    string? Slug,
    string? PackageName,
    ProductType Type,
    IReadOnlyList<ProductPriceInput> Prices,
    int? ServerGroupId);
