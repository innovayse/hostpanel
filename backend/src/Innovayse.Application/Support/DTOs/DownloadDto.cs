namespace Innovayse.Application.Support.DTOs;

/// <summary>DTO representing a downloadable file.</summary>
/// <param name="Id">Download primary key.</param>
/// <param name="Title">Display title.</param>
/// <param name="Description">Download description text.</param>
/// <param name="Type">File type label (e.g. "ZIP File", "PDF").</param>
/// <param name="Filename">Stored filename on disk or object storage.</param>
/// <param name="CategoryId">FK to the download category.</param>
/// <param name="CategoryName">Resolved category display name, or null if not resolved.</param>
/// <param name="DownloadCount">Total number of times this file has been downloaded.</param>
/// <param name="ClientsOnly">Whether the download is restricted to authenticated clients.</param>
/// <param name="ProductDownload">Whether the download is associated with a product.</param>
/// <param name="IsHidden">Whether the download is hidden from listings.</param>
/// <param name="CreatedAt">UTC timestamp when the download was created.</param>
public record DownloadDto(
    int Id,
    string Title,
    string Description,
    string Type,
    string Filename,
    int CategoryId,
    string? CategoryName,
    int DownloadCount,
    bool ClientsOnly,
    bool ProductDownload,
    bool IsHidden,
    DateTimeOffset CreatedAt);
