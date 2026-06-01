namespace Innovayse.Domain.Support;

using Innovayse.Domain.Common;

/// <summary>
/// Represents a download category used to organise downloadable files.
/// Categories can be hidden from public view and support hierarchical nesting.
/// </summary>
public sealed class DownloadCategory : Entity
{
    /// <summary>Gets the display name of this category.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the description of this category.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether this category is hidden from clients.</summary>
    public bool IsHidden { get; private set; }

    /// <summary>Gets the FK to the parent category for nesting, or null if top-level.</summary>
    public int? ParentCategoryId { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private DownloadCategory() : base(0) { }

    /// <summary>
    /// Creates a new <see cref="DownloadCategory"/> with the provided details.
    /// </summary>
    /// <param name="name">The category display name.</param>
    /// <param name="description">The category description.</param>
    /// <param name="isHidden">Whether the category should be hidden from clients.</param>
    /// <param name="parentCategoryId">Optional parent category ID for nesting.</param>
    /// <returns>A new <see cref="DownloadCategory"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public static DownloadCategory Create(string name, string description, bool isHidden, int? parentCategoryId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new DownloadCategory
        {
            Name = name,
            Description = description ?? string.Empty,
            IsHidden = isHidden,
            ParentCategoryId = parentCategoryId,
        };
    }

    /// <summary>
    /// Updates the details of this category.
    /// </summary>
    /// <param name="name">The new category display name.</param>
    /// <param name="description">The new category description.</param>
    /// <param name="isHidden">Whether the category should be hidden from clients.</param>
    /// <param name="parentCategoryId">Optional parent category ID for nesting.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public void Update(string name, string description, bool isHidden, int? parentCategoryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Description = description ?? string.Empty;
        IsHidden = isHidden;
        ParentCategoryId = parentCategoryId;
    }
}
