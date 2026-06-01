namespace Innovayse.Application.Support.Commands.DeleteDownload;

/// <summary>Command to delete a download by ID.</summary>
/// <param name="Id">The download identifier to delete.</param>
public record DeleteDownloadCommand(int Id);
