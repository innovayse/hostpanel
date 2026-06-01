namespace Innovayse.Application.Support.Queries.ListDownloads;

using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Returns downloads optionally filtered by category, with resolved category names.
/// </summary>
public sealed class ListDownloadsHandler(IDownloadRepository repo)
{
    /// <summary>
    /// Handles <see cref="ListDownloadsQuery"/>.
    /// </summary>
    /// <param name="query">The list downloads query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of download DTOs with resolved category names.</returns>
    public async Task<IReadOnlyList<DownloadDto>> HandleAsync(ListDownloadsQuery query, CancellationToken ct)
    {
        var downloads = await repo.ListByCategoryAsync(query.CategoryId, ct);
        var categories = await repo.ListCategoriesAsync(ct);

        var categoryNames = categories.ToDictionary(c => c.Id, c => c.Name);

        return downloads
            .Select(d => new DownloadDto(
                d.Id,
                d.Title,
                d.Description,
                d.Type,
                d.Filename,
                d.CategoryId,
                categoryNames.GetValueOrDefault(d.CategoryId),
                d.DownloadCount,
                d.ClientsOnly,
                d.ProductDownload,
                d.IsHidden,
                d.CreatedAt))
            .ToList();
    }
}
