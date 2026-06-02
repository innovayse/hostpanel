namespace Innovayse.Application.Support.Commands.UpdateDownloadCategory;

/// <summary>Command to update an existing download category.</summary>
/// <param name="Id">The category identifier.</param>
/// <param name="Name">The new category display name.</param>
/// <param name="Description">The new category description text.</param>
/// <param name="IsHidden">Whether the category should be hidden from clients.</param>
/// <param name="ParentCategoryId">Optional parent category ID for nesting.</param>
public record UpdateDownloadCategoryCommand(int Id, string Name, string Description, bool IsHidden, int? ParentCategoryId);
