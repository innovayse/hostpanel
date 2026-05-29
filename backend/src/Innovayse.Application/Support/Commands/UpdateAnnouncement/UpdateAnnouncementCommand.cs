namespace Innovayse.Application.Support.Commands.UpdateAnnouncement;

/// <summary>Command to update an existing announcement.</summary>
/// <param name="Id">The announcement identifier.</param>
/// <param name="Title">The new announcement title.</param>
/// <param name="Content">The new announcement body content.</param>
/// <param name="IsPublished">The new published state.</param>
public record UpdateAnnouncementCommand(int Id, string Title, string Content, bool IsPublished);
