namespace Innovayse.Domain.Support;

using Innovayse.Domain.Common;

/// <summary>
/// Represents a single reply message on a support ticket, either by a client or staff member.
/// Created via <see cref="Create"/> factory — no public constructor.
/// </summary>
public sealed class TicketReply : Entity
{
    /// <summary>Gets the body text of this reply.</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>Gets the display name of the author who posted this reply.</summary>
    public string AuthorName { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether this reply was posted by a staff member.</summary>
    public bool IsStaffReply { get; private set; }

    /// <summary>Gets the UTC timestamp when this reply was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private TicketReply() : base(0) { }

    /// <summary>Internal constructor used by the <see cref="Create"/> factory method.</summary>
    /// <param name="message">The reply body text.</param>
    /// <param name="authorName">Display name of the author.</param>
    /// <param name="isStaffReply">Whether the author is a staff member.</param>
    internal TicketReply(string message, string authorName, bool isStaffReply) : base(0)
    {
        Message = message;
        AuthorName = authorName;
        IsStaffReply = isStaffReply;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Creates a new <see cref="TicketReply"/> with the provided content and author.
    /// </summary>
    /// <param name="message">The reply body text.</param>
    /// <param name="authorName">Display name of the author.</param>
    /// <param name="isStaffReply">Whether the author is a staff member.</param>
    /// <returns>A new <see cref="TicketReply"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="message"/> or <paramref name="authorName"/> is null or whitespace.</exception>
    public static TicketReply Create(string message, string authorName, bool isStaffReply)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);
        return new TicketReply(message, authorName, isStaffReply);
    }
}
