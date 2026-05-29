namespace Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Repository abstraction for <see cref="KbCategory"/> entity persistence.
/// Implementations live in the Infrastructure layer.
/// </summary>
public interface IKbCategoryRepository
{
    /// <summary>
    /// Finds a category by its primary key.
    /// </summary>
    /// <param name="id">The category identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="KbCategory"/>, or <see langword="null"/> if not found.</returns>
    Task<KbCategory?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Stages a new category for insertion on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="category">The category to add.</param>
    void Add(KbCategory category);

    /// <summary>
    /// Stages a category for deletion on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="category">The category to remove.</param>
    void Remove(KbCategory category);

    /// <summary>
    /// Returns all knowledge base categories ordered by name.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of all categories.</returns>
    Task<IReadOnlyList<KbCategory>> ListAllAsync(CancellationToken ct);
}
