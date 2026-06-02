namespace Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Repository abstraction for <see cref="Download"/> and <see cref="DownloadCategory"/> entity persistence.
/// Implementations live in the Infrastructure layer.
/// </summary>
public interface IDownloadRepository
{
    /// <summary>
    /// Finds a download by its primary key.
    /// </summary>
    /// <param name="id">The download identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="Download"/>, or <see langword="null"/> if not found.</returns>
    Task<Download?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Stages a new download for insertion on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="download">The download to add.</param>
    void Add(Download download);

    /// <summary>
    /// Stages a download for deletion on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="download">The download to remove.</param>
    void Remove(Download download);

    /// <summary>
    /// Returns downloads filtered by category, ordered by creation date descending.
    /// When <paramref name="categoryId"/> is <see langword="null"/>, returns all downloads.
    /// </summary>
    /// <param name="categoryId">Optional category filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of downloads.</returns>
    Task<IReadOnlyList<Download>> ListByCategoryAsync(int? categoryId, CancellationToken ct);

    /// <summary>
    /// Finds a download category by its primary key.
    /// </summary>
    /// <param name="id">The category identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="DownloadCategory"/>, or <see langword="null"/> if not found.</returns>
    Task<DownloadCategory?> FindCategoryByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Stages a new category for insertion on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="category">The category to add.</param>
    void AddCategory(DownloadCategory category);

    /// <summary>
    /// Stages a category for deletion on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="category">The category to remove.</param>
    void RemoveCategory(DownloadCategory category);

    /// <summary>
    /// Returns all download categories ordered by name.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of all categories.</returns>
    Task<IReadOnlyList<DownloadCategory>> ListCategoriesAsync(CancellationToken ct);

    /// <summary>
    /// Counts the number of downloads in a specific category.
    /// </summary>
    /// <param name="categoryId">The category identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of downloads in the category.</returns>
    Task<int> CountByCategoryIdAsync(int categoryId, CancellationToken ct);
}
