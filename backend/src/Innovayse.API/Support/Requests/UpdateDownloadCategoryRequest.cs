namespace Innovayse.API.Support.Requests;

/// <summary>Request body for updating a download category.</summary>
/// <param name="Name">Updated category display name.</param>
/// <param name="Description">Updated category description text.</param>
/// <param name="IsHidden">Whether the category should be hidden from clients.</param>
/// <param name="ParentCategoryId">Optional parent category ID for nesting.</param>
public record UpdateDownloadCategoryRequest(string Name, string Description, bool IsHidden, int? ParentCategoryId);
