namespace Innovayse.Application.Products.Queries.GetProductGroups;

/// <summary>Represents a product group in list responses.</summary>
/// <param name="Id">Primary key.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="IsActive">Whether the group is visible in the storefront.</param>
/// <param name="ProductCount">Number of products in the group.</param>
public record ProductGroupDto(int Id, string Name, string? Description, bool IsActive, int ProductCount);
