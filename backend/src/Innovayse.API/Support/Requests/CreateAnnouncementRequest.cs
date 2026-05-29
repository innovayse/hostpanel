namespace Innovayse.API.Support.Requests;

/// <summary>Request body for creating a new announcement.</summary>
public sealed class CreateAnnouncementRequest
{
    /// <summary>Gets the announcement title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the announcement body content.</summary>
    public required string Content { get; init; }

    /// <summary>Gets a value indicating whether the announcement should be immediately published.</summary>
    public required bool IsPublished { get; init; }
}
