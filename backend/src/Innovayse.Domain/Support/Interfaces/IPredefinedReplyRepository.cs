namespace Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Repository abstraction for <see cref="PredefinedReplyCategory"/> and <see cref="PredefinedReply"/> persistence.
/// Implementations live in the Infrastructure layer.
/// </summary>
public interface IPredefinedReplyRepository
{
    /// <summary>
    /// Finds a predefined reply category by its primary key.
    /// </summary>
    /// <param name="id">The category identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="PredefinedReplyCategory"/>, or <see langword="null"/> if not found.</returns>
    Task<PredefinedReplyCategory?> FindCategoryByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Stages a new category for insertion on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="category">The category to add.</param>
    void AddCategory(PredefinedReplyCategory category);

    /// <summary>
    /// Stages a category for deletion on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="category">The category to remove.</param>
    void RemoveCategory(PredefinedReplyCategory category);

    /// <summary>
    /// Returns all predefined reply categories with their reply counts.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of all categories with reply counts as tuples.</returns>
    Task<IReadOnlyList<(PredefinedReplyCategory Category, int ReplyCount)>> ListCategoriesAsync(CancellationToken ct);

    /// <summary>
    /// Finds a predefined reply by its primary key.
    /// </summary>
    /// <param name="id">The reply identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="PredefinedReply"/>, or <see langword="null"/> if not found.</returns>
    Task<PredefinedReply?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Stages a new predefined reply for insertion on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="reply">The reply to add.</param>
    void Add(PredefinedReply reply);

    /// <summary>
    /// Stages a predefined reply for deletion on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="reply">The reply to remove.</param>
    void Remove(PredefinedReply reply);

    /// <summary>
    /// Returns all predefined replies, optionally filtered by category.
    /// </summary>
    /// <param name="categoryId">Optional category FK filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of replies with their category names.</returns>
    Task<IReadOnlyList<(PredefinedReply Reply, string? CategoryName)>> ListAsync(int? categoryId, CancellationToken ct);

    /// <summary>
    /// Searches predefined replies by name or content.
    /// </summary>
    /// <param name="query">The search term to match against name and content.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching replies with their category names.</returns>
    Task<IReadOnlyList<(PredefinedReply Reply, string? CategoryName)>> SearchAsync(string query, CancellationToken ct);
}
