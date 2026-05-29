namespace Innovayse.Application.Support.Commands.DeleteAnnouncement;

/// <summary>Command to permanently delete an announcement.</summary>
/// <param name="Id">The announcement identifier.</param>
public record DeleteAnnouncementCommand(int Id);
