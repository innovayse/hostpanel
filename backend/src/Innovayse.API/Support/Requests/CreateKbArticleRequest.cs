namespace Innovayse.API.Support.Requests;

/// <summary>Request body for creating a knowledge base article.</summary>
public sealed class CreateKbArticleRequest
{
    /// <summary>Gets or sets the article title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets or sets the article body content.</summary>
    public required string Content { get; init; }

    /// <summary>Gets or sets the category name.</summary>
    public required string Category { get; init; }
}
