namespace Innovayse.Application.Products.Commands.UpdateProduct;

/// <summary>Command to update an existing product's details and prices.</summary>
/// <param name="Id">Product primary key.</param>
/// <param name="Name">New display name.</param>
/// <param name="Description">New description.</param>
/// <param name="Website">Website URL for the product's landing page, or null to clear.</param>
/// <param name="Slug">URL-friendly slug, or null to clear.</param>
/// <param name="MonthlyPrice">New monthly price.</param>
/// <param name="AnnualPrice">New annual price.</param>
/// <param name="DeployRepoUrl">Git repository URL for managed site deployment, or null to clear.</param>
/// <param name="DeployBranch">Git branch to clone, or null to clear.</param>
/// <param name="DeployScript">Shell script to run after cloning, or null to clear.</param>
public record UpdateProductCommand(
    int Id,
    string Name,
    string? Description,
    string? Website,
    string? Slug,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    string? DeployRepoUrl,
    string? DeployBranch,
    string? DeployScript);
