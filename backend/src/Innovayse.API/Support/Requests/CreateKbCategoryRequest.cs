namespace Innovayse.API.Support.Requests;

/// <summary>Request body for creating a knowledge base category.</summary>
/// <param name="Name">Category display name.</param>
/// <param name="Description">Category description text.</param>
/// <param name="IsHidden">Whether the category should be hidden from clients.</param>
/// <param name="ParentCategoryId">Optional parent category ID for nesting.</param>
public record CreateKbCategoryRequest(string Name, string Description, bool IsHidden, int? ParentCategoryId = null);
