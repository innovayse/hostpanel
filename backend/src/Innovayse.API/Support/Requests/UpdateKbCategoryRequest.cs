namespace Innovayse.API.Support.Requests;

/// <summary>Request body for updating a knowledge base category.</summary>
/// <param name="Name">Updated category display name.</param>
/// <param name="Description">Updated category description text.</param>
/// <param name="IsHidden">Whether the category should be hidden from clients.</param>
public record UpdateKbCategoryRequest(string Name, string Description, bool IsHidden);
