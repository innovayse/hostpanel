namespace Innovayse.API.Support.Requests;

/// <summary>Request body for creating a predefined reply category.</summary>
public sealed class CreatePredefinedReplyCategoryRequest
{
    /// <summary>Gets the category name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the FK to the parent category, or null for a root category.</summary>
    public int? ParentCategoryId { get; init; }
}
