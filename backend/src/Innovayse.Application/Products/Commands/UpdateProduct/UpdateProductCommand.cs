namespace Innovayse.Application.Products.Commands.UpdateProduct;

/// <summary>Command to update an existing product's details and prices.</summary>
/// <param name="Id">Product primary key.</param>
/// <param name="Name">New display name.</param>
/// <param name="Description">New description.</param>
/// <param name="Website">Website URL for the product's landing page, or null to clear.</param>
/// <param name="MonthlyPrice">New monthly price.</param>
/// <param name="AnnualPrice">New annual price.</param>
public record UpdateProductCommand(int Id, string Name, string? Description, string? Website, decimal MonthlyPrice, decimal AnnualPrice);
