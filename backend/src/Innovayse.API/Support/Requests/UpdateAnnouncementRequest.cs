namespace Innovayse.API.Support.Requests;

/// <summary>Request body for updating an existing announcement.</summary>
public sealed class UpdateAnnouncementRequest
{
    /// <summary>Gets the new announcement title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the new announcement body content.</summary>
    public required string Content { get; init; }

    /// <summary>Gets a value indicating whether the announcement should be published.</summary>
    public required bool IsPublished { get; init; }
}
