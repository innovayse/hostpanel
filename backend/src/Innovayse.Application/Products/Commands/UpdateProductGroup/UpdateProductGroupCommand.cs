namespace Innovayse.Application.Products.Commands.UpdateProductGroup;

/// <summary>Command to update a product group's name and description.</summary>
/// <param name="Id">Product group primary key.</param>
/// <param name="Name">New display name.</param>
/// <param name="Description">New description.</param>
public record UpdateProductGroupCommand(int Id, string Name, string? Description);
