namespace Innovayse.Application.Support.Queries.GetDownload;

using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Loads a single download and maps it to <see cref="DownloadDto"/>.
/// </summary>
public sealed class GetDownloadHandler(IDownloadRepository repo)
{
    /// <summary>
    /// Handles <see cref="GetDownloadQuery"/>.
    /// </summary>
    /// <param name="query">The get download query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="DownloadDto"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the download is not found.</exception>
    public async Task<DownloadDto> HandleAsync(GetDownloadQuery query, CancellationToken ct)
    {
        var download = await repo.FindByIdAsync(query.Id, ct)
            ?? throw new InvalidOperationException($"Download {query.Id} not found.");

        var category = await repo.FindCategoryByIdAsync(download.CategoryId, ct);

        return new DownloadDto(
            download.Id,
            download.Title,
            download.Description,
            download.Type,
            download.Filename,
            download.CategoryId,
            category?.Name,
            download.DownloadCount,
            download.ClientsOnly,
            download.ProductDownload,
            download.IsHidden,
            download.CreatedAt);
    }
}
