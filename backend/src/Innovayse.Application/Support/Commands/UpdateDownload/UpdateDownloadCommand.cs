namespace Innovayse.Application.Support.Commands.UpdateDownload;

/// <summary>Command to update an existing download entry.</summary>
/// <param name="Id">The download identifier.</param>
/// <param name="Title">The new display title.</param>
/// <param name="Description">The new description text.</param>
/// <param name="Type">The new file type label.</param>
/// <param name="Filename">The new stored filename.</param>
/// <param name="CategoryId">The new category identifier.</param>
/// <param name="ClientsOnly">Whether the download is restricted to authenticated clients.</param>
/// <param name="ProductDownload">Whether the download is associated with a product.</param>
/// <param name="IsHidden">Whether the download is hidden from listings.</param>
public record UpdateDownloadCommand(
    int Id,
    string Title,
    string Description,
    string Type,
    string Filename,
    int CategoryId,
    bool ClientsOnly,
    bool ProductDownload,
    bool IsHidden);
