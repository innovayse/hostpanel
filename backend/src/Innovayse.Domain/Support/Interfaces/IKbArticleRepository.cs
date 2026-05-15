namespace Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Repository abstraction for <see cref="KbArticle"/> entity persistence.
/// Implementations live in the Infrastructure layer.
/// </summary>
public interface IKbArticleRepository
{
    /// <summary>
    /// Finds a knowledge base article by its primary key.
    /// </summary>
    /// <param name="id">The article identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="KbArticle"/>, or <see langword="null"/> if not found.</returns>
    Task<KbArticle?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Stages a new article for insertion on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="article">The article to add.</param>
    void Add(KbArticle article);

    /// <summary>
    /// Marks an existing article as modified on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="article">The article to update.</param>
    void Update(KbArticle article);

    /// <summary>
    /// Stages an article for deletion on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="article">The article to delete.</param>
    void Delete(KbArticle article);

    /// <summary>
    /// Returns all knowledge base articles regardless of published state.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of all articles.</returns>
    Task<IReadOnlyList<KbArticle>> ListAllAsync(CancellationToken ct);

    /// <summary>
    /// Returns only published knowledge base articles visible to clients.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of published articles.</returns>
    Task<IReadOnlyList<KbArticle>> ListPublishedAsync(CancellationToken ct);
}
