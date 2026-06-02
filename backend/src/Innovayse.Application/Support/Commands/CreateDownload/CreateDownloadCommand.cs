namespace Innovayse.Application.Support.Commands.CreateDownload;

/// <summary>Command to create a new download entry.</summary>
/// <param name="Title">The display title.</param>
/// <param name="Description">The download description text.</param>
/// <param name="Type">The file type label (e.g. "ZIP File", "PDF").</param>
/// <param name="Filename">The stored filename.</param>
/// <param name="CategoryId">The category identifier.</param>
/// <param name="ClientsOnly">Whether the download is restricted to authenticated clients.</param>
/// <param name="ProductDownload">Whether the download is associated with a product.</param>
/// <param name="IsHidden">Whether the download is hidden from listings.</param>
public record CreateDownloadCommand(
    string Title,
    string Description,
    string Type,
    string Filename,
    int CategoryId,
    bool ClientsOnly,
    bool ProductDownload,
    bool IsHidden);
