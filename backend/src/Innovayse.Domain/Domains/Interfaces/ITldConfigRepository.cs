namespace Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Persistence contract for the <see cref="TldConfig"/> entity.
/// Implemented in Infrastructure by EF Core.
/// </summary>
public interface ITldConfigRepository
{
    /// <summary>
    /// Finds a TLD configuration by primary key.
    /// </summary>
    /// <param name="id">The TLD configuration identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="TldConfig"/>, or <see langword="null"/> if not found.</returns>
    Task<TldConfig?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Finds a TLD configuration by its TLD extension.
    /// </summary>
    /// <param name="tld">The TLD extension without the leading dot (e.g. "am", "com").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="TldConfig"/>, or <see langword="null"/> if not found.</returns>
    Task<TldConfig?> FindByTldAsync(string tld, CancellationToken ct);

    /// <summary>
    /// Returns all TLD configurations ordered by sort order.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All TLD configurations.</returns>
    Task<List<TldConfig>> ListAllAsync(CancellationToken ct);

    /// <summary>
    /// Returns all enabled TLD configurations ordered by sort order.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Enabled TLD configurations available for sale to clients.</returns>
    Task<List<TldConfig>> ListEnabledAsync(CancellationToken ct);

    /// <summary>
    /// Returns all TLD configurations managed by the specified registrar module.
    /// </summary>
    /// <param name="module">The registrar module to filter by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>TLD configurations for the given registrar module.</returns>
    Task<List<TldConfig>> ListByRegistrarAsync(RegistrarModule module, CancellationToken ct);

    /// <summary>
    /// Adds a new TLD configuration to the persistence store.
    /// Call <c>SaveChangesAsync</c> after adding to persist.
    /// </summary>
    /// <param name="config">The TLD configuration to add.</param>
    void Add(TldConfig config);

    /// <summary>
    /// Removes a TLD configuration from the persistence store.
    /// Call <c>SaveChangesAsync</c> after removing to persist.
    /// </summary>
    /// <param name="config">The TLD configuration to remove.</param>
    void Remove(TldConfig config);
}
