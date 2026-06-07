namespace Innovayse.Application.Products.Commands.CreateProduct;

using Innovayse.Domain.Products;

/// <summary>Command to create a new product in a product group.</summary>
/// <param name="GroupId">FK to the parent product group.</param>
/// <param name="Name">Product display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Website">Optional website URL for the product's landing page.</param>
/// <param name="Slug">Optional URL-friendly slug for the product.</param>
/// <param name="Type">Product type.</param>
/// <param name="MonthlyPrice">Monthly price (≥ 0).</param>
/// <param name="AnnualPrice">Annual price (≥ 0).</param>
/// <param name="DeployRepoUrl">Optional Git repository URL for managed site deployment.</param>
/// <param name="DeployBranch">Optional Git branch to clone.</param>
/// <param name="DeployScript">Optional shell script to run after cloning the repo.</param>
public record CreateProductCommand(
    int GroupId,
    string Name,
    string? Description,
    string? Website,
    string? Slug,
    ProductType Type,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    string? DeployRepoUrl,
    string? DeployBranch,
    string? DeployScript);
