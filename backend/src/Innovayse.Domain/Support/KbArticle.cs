namespace Innovayse.Domain.Support;

using Innovayse.Domain.Common;

/// <summary>
/// Represents a knowledge base article that can be published and made visible to clients.
/// Created via <see cref="Create"/> factory — no public constructor.
/// </summary>
public sealed class KbArticle : Entity
{
    /// <summary>Gets the article title.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Gets the full article body content (supports Markdown or HTML).</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>Gets the category this article belongs to.</summary>
    public string Category { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether this article is visible to clients.</summary>
    public bool IsPublished { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private KbArticle() : base(0) { }

    /// <summary>Internal constructor — use <see cref="Create"/> factory instead.</summary>
    /// <param name="title">The article title.</param>
    /// <param name="content">The article body content.</param>
    /// <param name="category">The category name.</param>
    private KbArticle(string title, string content, string category) : base(0)
    {
        Title = title;
        Content = content;
        Category = category;
        IsPublished = false;
    }

    /// <summary>
    /// Creates a new unpublished <see cref="KbArticle"/> with the provided content.
    /// </summary>
    /// <param name="title">The article title.</param>
    /// <param name="content">The article body content.</param>
    /// <param name="category">The category name.</param>
    /// <returns>A new unpublished <see cref="KbArticle"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when any argument is null or whitespace.</exception>
    public static KbArticle Create(string title, string content, string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        return new KbArticle(title, content, category);
    }

    /// <summary>
    /// Updates the title, content, and category of this article.
    /// </summary>
    /// <param name="title">The new article title.</param>
    /// <param name="content">The new article body content.</param>
    /// <param name="category">The new category name.</param>
    /// <exception cref="ArgumentException">Thrown when any argument is null or whitespace.</exception>
    public void Update(string title, string content, string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        Title = title;
        Content = content;
        Category = category;
    }

    /// <summary>
    /// Publishes this article, making it visible to clients.
    /// </summary>
    public void Publish() => IsPublished = true;

    /// <summary>
    /// Unpublishes this article, hiding it from clients.
    /// </summary>
    public void Unpublish() => IsPublished = false;
}
