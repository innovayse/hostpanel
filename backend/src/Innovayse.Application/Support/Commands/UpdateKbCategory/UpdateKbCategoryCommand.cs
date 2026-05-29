namespace Innovayse.Application.Support.Commands.UpdateKbCategory;

/// <summary>Command to update an existing knowledge base category.</summary>
/// <param name="Id">The category identifier.</param>
/// <param name="Name">The new category display name.</param>
/// <param name="Description">The new category description text.</param>
/// <param name="IsHidden">Whether the category should be hidden from clients.</param>
public record UpdateKbCategoryCommand(int Id, string Name, string Description, bool IsHidden);
