namespace Innovayse.Domain.Support;

using Innovayse.Domain.Common;

/// <summary>
/// Represents a pre-written reply that support staff can insert into ticket responses.
/// Belongs to a <see cref="PredefinedReplyCategory"/>.
/// Created via <see cref="Create"/> factory — no public constructor.
/// </summary>
public sealed class PredefinedReply : Entity
{
    /// <summary>Gets the reply name / label (max 255 characters).</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the reply content text.</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>Gets the FK to the category this reply belongs to.</summary>
    public int CategoryId { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private PredefinedReply() : base(0) { }

    /// <summary>
    /// Creates a new <see cref="PredefinedReply"/>.
    /// </summary>
    /// <param name="name">The reply name / label.</param>
    /// <param name="content">The reply content text.</param>
    /// <param name="categoryId">FK to the category.</param>
    /// <returns>A new <see cref="PredefinedReply"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="content"/> is null or whitespace.</exception>
    public static PredefinedReply Create(string name, string content, int categoryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        return new PredefinedReply
        {
            Name = name,
            Content = content,
            CategoryId = categoryId,
        };
    }

    /// <summary>
    /// Updates the mutable fields of this predefined reply.
    /// </summary>
    /// <param name="name">The new reply name.</param>
    /// <param name="content">The new reply content.</param>
    /// <param name="categoryId">The new category FK.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="content"/> is null or whitespace.</exception>
    public void Update(string name, string content, int categoryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        Name = name;
        Content = content;
        CategoryId = categoryId;
    }
}
