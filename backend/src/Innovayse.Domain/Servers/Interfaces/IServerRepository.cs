namespace Innovayse.Domain.Servers.Interfaces;

/// <summary>
/// Persistence abstraction for <see cref="Server"/> aggregates.
/// </summary>
public interface IServerRepository
{
    /// <summary>Returns a server by its identifier, or null if not found.</summary>
    /// <param name="id">The server identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching server, or null.</returns>
    Task<Server?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>Returns all servers, optionally filtered by module type.</summary>
    /// <param name="module">Optional module filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching servers.</returns>
    Task<List<Server>> ListAsync(ServerModule? module, CancellationToken ct);

    /// <summary>Returns servers matching the supplied identifiers.</summary>
    /// <param name="ids">Server identifiers to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching servers (may be fewer than requested if some IDs don't exist).</returns>
    Task<IReadOnlyList<Server>> FindByIdsAsync(IEnumerable<int> ids, CancellationToken ct);

    /// <summary>Queues a new server for insertion on the next SaveChanges.</summary>
    /// <param name="server">The server to add.</param>
    void Add(Server server);

    /// <summary>Queues a server for deletion on the next SaveChanges.</summary>
    /// <param name="server">The server to remove.</param>
    void Remove(Server server);
}
