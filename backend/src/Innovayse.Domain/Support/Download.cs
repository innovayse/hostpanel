namespace Innovayse.Domain.Support;

using Innovayse.Domain.Common;

/// <summary>
/// Represents a downloadable file that can be associated with a category
/// and optionally restricted to authenticated clients or linked to products.
/// </summary>
public sealed class Download : Entity
{
    /// <summary>Gets the display title of this download.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Gets the description of this download.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Gets the file type label (e.g. "ZIP File", "Executable", "PDF").</summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>Gets the stored filename on disk or object storage.</summary>
    public string Filename { get; private set; } = string.Empty;

    /// <summary>Gets the FK to the category this download belongs to.</summary>
    public int CategoryId { get; private set; }

    /// <summary>Gets the total number of times this file has been downloaded.</summary>
    public int DownloadCount { get; private set; }

    /// <summary>Gets a value indicating whether this download is restricted to authenticated clients.</summary>
    public bool ClientsOnly { get; private set; }

    /// <summary>Gets a value indicating whether this download is associated with a product.</summary>
    public bool ProductDownload { get; private set; }

    /// <summary>Gets a value indicating whether this download is hidden from listings.</summary>
    public bool IsHidden { get; private set; }

    /// <summary>Gets the UTC timestamp when this download was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Download() : base(0) { }

    /// <summary>
    /// Creates a new <see cref="Download"/> with the provided details.
    /// </summary>
    /// <param name="title">The display title.</param>
    /// <param name="description">The download description.</param>
    /// <param name="type">The file type label.</param>
    /// <param name="filename">The stored filename.</param>
    /// <param name="categoryId">The category identifier.</param>
    /// <param name="clientsOnly">Whether the download is restricted to clients.</param>
    /// <param name="productDownload">Whether the download is associated with a product.</param>
    /// <param name="isHidden">Whether the download is hidden from listings.</param>
    /// <returns>A new <see cref="Download"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="title"/> is null or whitespace.</exception>
    public static Download Create(
        string title,
        string description,
        string type,
        string filename,
        int categoryId,
        bool clientsOnly,
        bool productDownload,
        bool isHidden)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new Download
        {
            Title = title,
            Description = description ?? string.Empty,
            Type = type ?? "ZIP File",
            Filename = filename ?? string.Empty,
            CategoryId = categoryId,
            ClientsOnly = clientsOnly,
            ProductDownload = productDownload,
            IsHidden = isHidden,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Updates the details of this download.
    /// </summary>
    /// <param name="title">The new display title.</param>
    /// <param name="description">The new description.</param>
    /// <param name="type">The new file type label.</param>
    /// <param name="filename">The new stored filename.</param>
    /// <param name="categoryId">The new category identifier.</param>
    /// <param name="clientsOnly">Whether the download is restricted to clients.</param>
    /// <param name="productDownload">Whether the download is associated with a product.</param>
    /// <param name="isHidden">Whether the download is hidden from listings.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="title"/> is null or whitespace.</exception>
    public void Update(
        string title,
        string description,
        string type,
        string filename,
        int categoryId,
        bool clientsOnly,
        bool productDownload,
        bool isHidden)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title;
        Description = description ?? string.Empty;
        Type = type ?? "ZIP File";
        Filename = filename ?? string.Empty;
        CategoryId = categoryId;
        ClientsOnly = clientsOnly;
        ProductDownload = productDownload;
        IsHidden = isHidden;
    }

    /// <summary>Increments the download counter by one.</summary>
    public void IncrementDownloadCount() => DownloadCount++;
}
