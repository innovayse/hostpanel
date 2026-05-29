namespace Innovayse.Domain.Support;

using Innovayse.Domain.Common;

/// <summary>
/// Represents an announcement that can be published and made visible to clients.
/// Created via <see cref="Create"/> factory — no public constructor.
/// </summary>
public sealed class Announcement : Entity
{
    /// <summary>Gets the announcement title.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Gets the full announcement body content (supports Markdown or HTML).</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether this announcement is visible to clients.</summary>
    public bool IsPublished { get; private set; }

    /// <summary>Gets the UTC timestamp when this announcement was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Announcement() : base(0) { }

    /// <summary>Internal constructor — use <see cref="Create"/> factory instead.</summary>
    /// <param name="title">The announcement title.</param>
    /// <param name="content">The announcement body content.</param>
    /// <param name="isPublished">Whether the announcement should be immediately published.</param>
    private Announcement(string title, string content, bool isPublished) : base(0)
    {
        Title = title;
        Content = content;
        IsPublished = isPublished;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Creates a new <see cref="Announcement"/> with the provided content.
    /// </summary>
    /// <param name="title">The announcement title.</param>
    /// <param name="content">The announcement body content.</param>
    /// <param name="isPublished">Whether the announcement should be immediately published.</param>
    /// <returns>A new <see cref="Announcement"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="title"/> is null or whitespace.</exception>
    public static Announcement Create(string title, string content, bool isPublished)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new Announcement(title, content, isPublished);
    }

    /// <summary>
    /// Updates the title, content, and published state of this announcement.
    /// </summary>
    /// <param name="title">The new announcement title.</param>
    /// <param name="content">The new announcement body content.</param>
    /// <param name="isPublished">The new published state.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="title"/> is null or whitespace.</exception>
    public void Update(string title, string content, bool isPublished)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title;
        Content = content;
        IsPublished = isPublished;
    }
}
