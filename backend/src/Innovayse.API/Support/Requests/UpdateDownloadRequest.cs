namespace Innovayse.API.Support.Requests;

/// <summary>Request body for updating a download entry.</summary>
/// <param name="Title">Updated display title.</param>
/// <param name="Description">Updated description text.</param>
/// <param name="Type">Updated file type label.</param>
/// <param name="Filename">Updated stored filename.</param>
/// <param name="CategoryId">Updated category identifier.</param>
/// <param name="ClientsOnly">Whether the download is restricted to authenticated clients.</param>
/// <param name="ProductDownload">Whether the download is associated with a product.</param>
/// <param name="IsHidden">Whether the download is hidden from listings.</param>
public record UpdateDownloadRequest(
    string Title,
    string Description,
    string Type,
    string Filename,
    int CategoryId,
    bool ClientsOnly,
    bool ProductDownload,
    bool IsHidden);
