namespace Innovayse.Application.Support.Commands.CreateAnnouncement;

/// <summary>Command to create a new announcement.</summary>
/// <param name="Title">The announcement title.</param>
/// <param name="Content">The announcement body content.</param>
/// <param name="IsPublished">Whether the announcement should be immediately published.</param>
public record CreateAnnouncementCommand(string Title, string Content, bool IsPublished);
