namespace Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Repository for managing <see cref="ClientUser"/> links between Identity users and client accounts.
/// </summary>
public interface IClientUserRepository
{
    /// <summary>Finds a specific user-client link.</summary>
    /// <param name="userId">The Identity user ID.</param>
    /// <param name="clientId">The client account ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="ClientUser"/>, or <see langword="null"/> if not found.</returns>
    Task<ClientUser?> FindAsync(string userId, int clientId, CancellationToken ct);

    /// <summary>Finds all users linked to a client (non-owner users only).</summary>
    /// <param name="clientId">The client account ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of linked users.</returns>
    Task<IReadOnlyList<ClientUser>> FindByClientIdAsync(int clientId, CancellationToken ct);

    /// <summary>Finds all clients a user is linked to (non-owner links only).</summary>
    /// <param name="userId">The Identity user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of client links.</returns>
    Task<IReadOnlyList<ClientUser>> FindByUserIdAsync(string userId, CancellationToken ct);

    /// <summary>Adds a new client-user link.</summary>
    /// <param name="clientUser">The client-user link to add.</param>
    void Add(ClientUser clientUser);

    /// <summary>Removes a client-user link.</summary>
    /// <param name="clientUser">The client-user link to remove.</param>
    void Remove(ClientUser clientUser);
}
