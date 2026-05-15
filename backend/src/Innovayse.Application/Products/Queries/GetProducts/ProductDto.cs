namespace Innovayse.Application.Products.Queries.GetProducts;

using Innovayse.Domain.Products;

/// <summary>Represents a product in list and detail responses.</summary>
/// <param name="Id">Primary key.</param>
/// <param name="GroupId">Parent product group ID.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Website">Optional website URL for the product's landing page.</param>
/// <param name="Type">Product type.</param>
/// <param name="Status">Current status.</param>
/// <param name="Pricing">Monthly and annual pricing.</param>
public record ProductDto(
    int Id,
    int GroupId,
    string Name,
    string? Description,
    string? Website,
    ProductType Type,
    ProductStatus Status,
    ProductPricingDto Pricing);
