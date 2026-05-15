namespace Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Persistence contract for the <see cref="Setting"/> aggregate.
/// Implemented in Infrastructure by EF Core.
/// </summary>
public interface ISettingRepository
{
    /// <summary>
    /// Finds a setting by its primary key.
    /// </summary>
    /// <param name="id">The setting primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="Setting"/>, or <see langword="null"/> if not found.</returns>
    Task<Setting?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Finds a setting by its unique key.
    /// </summary>
    /// <param name="key">The configuration key to search for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="Setting"/>, or <see langword="null"/> if not found.</returns>
    Task<Setting?> FindByKeyAsync(string key, CancellationToken ct);

    /// <summary>
    /// Adds a new setting. Call <c>IUnitOfWork.SaveChangesAsync</c> to persist.
    /// </summary>
    /// <param name="setting">The new setting to add.</param>
    void Add(Setting setting);

    /// <summary>
    /// Returns all settings ordered by key.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Read-only list of all settings.</returns>
    Task<IReadOnlyList<Setting>> ListAsync(CancellationToken ct);
}
