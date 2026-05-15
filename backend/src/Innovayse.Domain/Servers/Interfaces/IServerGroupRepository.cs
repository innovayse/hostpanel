namespace Innovayse.Domain.Servers.Interfaces;

/// <summary>
/// Persistence abstraction for <see cref="ServerGroup"/> aggregates.
/// </summary>
public interface IServerGroupRepository
{
    /// <summary>Returns a server group by its identifier, or null if not found.</summary>
    /// <param name="id">The group identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching group with its servers loaded, or null.</returns>
    Task<ServerGroup?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>Returns all server groups with their associated servers.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of all groups.</returns>
    Task<List<ServerGroup>> ListAsync(CancellationToken ct);

    /// <summary>Queues a new group for insertion on the next SaveChanges.</summary>
    /// <param name="group">The group to add.</param>
    void Add(ServerGroup group);

    /// <summary>Queues a group for deletion on the next SaveChanges.</summary>
    /// <param name="group">The group to remove.</param>
    void Remove(ServerGroup group);
}
