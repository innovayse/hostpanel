namespace Innovayse.Domain.Support;

using Innovayse.Domain.Common;

/// <summary>
/// Represents a category that groups predefined replies for support staff.
/// Supports one level of nesting via <see cref="ParentCategoryId"/>.
/// Created via <see cref="Create"/> factory — no public constructor.
/// </summary>
public sealed class PredefinedReplyCategory : Entity
{
    /// <summary>Gets the category name (max 255 characters).</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the FK to the parent category, or <see langword="null"/> if this is a root category.</summary>
    public int? ParentCategoryId { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private PredefinedReplyCategory() : base(0) { }

    /// <summary>
    /// Creates a new <see cref="PredefinedReplyCategory"/>.
    /// </summary>
    /// <param name="name">The category name.</param>
    /// <param name="parentCategoryId">FK to the parent category, or <see langword="null"/> for a root category.</param>
    /// <returns>A new <see cref="PredefinedReplyCategory"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public static PredefinedReplyCategory Create(string name, int? parentCategoryId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new PredefinedReplyCategory
        {
            Name = name,
            ParentCategoryId = parentCategoryId,
        };
    }

    /// <summary>
    /// Updates the category name.
    /// </summary>
    /// <param name="name">The new category name.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public void Update(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
}
