namespace Innovayse.API.Support.Requests;

/// <summary>Request body for creating a download entry.</summary>
/// <param name="Title">Display title.</param>
/// <param name="Description">Download description text.</param>
/// <param name="Type">File type label (e.g. "ZIP File", "PDF").</param>
/// <param name="Filename">Stored filename.</param>
/// <param name="CategoryId">Category identifier.</param>
/// <param name="ClientsOnly">Whether the download is restricted to authenticated clients.</param>
/// <param name="ProductDownload">Whether the download is associated with a product.</param>
/// <param name="IsHidden">Whether the download is hidden from listings.</param>
public record CreateDownloadRequest(
    string Title,
    string Description,
    string Type,
    string Filename,
    int CategoryId,
    bool ClientsOnly,
    bool ProductDownload,
    bool IsHidden);
