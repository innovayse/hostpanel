namespace Innovayse.Application.Clients.Services;

using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Service for resolving and enforcing user access to client accounts.
/// Handles both account owners and additional users with granular permissions.
/// </summary>
public interface IClientAccessService
{
    /// <summary>
    /// Resolves a user's access to a client account and verifies the required permission.
    /// If no client ID is provided, attempts to resolve the user's single linked account.
    /// </summary>
    /// <param name="userId">The authenticated Identity user ID.</param>
    /// <param name="clientId">Optional client ID. If null, auto-resolves the user's account.</param>
    /// <param name="required">The permission required for this operation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of resolved client ID and the user's granted permissions.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the client is not found or multiple accounts exist without a specified ID.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the user lacks access or the required permission.</exception>
    Task<(int ClientId, ClientPermission Permissions)> ResolveAccessAsync(
        string userId, int? clientId, ClientPermission required, CancellationToken ct);
}

/// <summary>
/// Implementation of <see cref="IClientAccessService"/> using client and client-user repositories.
/// </summary>
/// <param name="clientRepo">Client repository for owner lookups.</param>
/// <param name="clientUserRepo">Client-user link repository for additional user lookups.</param>
public sealed class ClientAccessService(
    IClientRepository clientRepo,
    IClientUserRepository clientUserRepo) : IClientAccessService
{
    /// <inheritdoc/>
    public async Task<(int ClientId, ClientPermission Permissions)> ResolveAccessAsync(
        string userId, int? clientId, ClientPermission required, CancellationToken ct)
    {
        // If clientId provided, check access to that specific client
        if (clientId.HasValue)
        {
            var client = await clientRepo.FindByIdAsync(clientId.Value, ct)
                ?? throw new InvalidOperationException($"Client {clientId.Value} not found.");

            // Owner has all permissions
            if (client.UserId == userId)
                return (client.Id, ClientPermission.All);

            // Check additional user link
            var link = await clientUserRepo.FindAsync(userId, clientId.Value, ct)
                ?? throw new UnauthorizedAccessException("You do not have access to this client account.");

            if (required != ClientPermission.None && !link.Permissions.HasFlag(required))
                throw new UnauthorizedAccessException($"You do not have the required permission: {required}.");

            return (client.Id, link.Permissions);
        }

        // No clientId — try to resolve the single client this user owns
        var ownedClient = await clientRepo.FindByUserIdAsync(userId, ct);
        if (ownedClient is not null)
            return (ownedClient.Id, ClientPermission.All);

        // Check if user has any additional links
        var links = await clientUserRepo.FindByUserIdAsync(userId, ct);
        if (links.Count == 1)
        {
            var link = links[0];
            if (required != ClientPermission.None && !link.Permissions.HasFlag(required))
                throw new UnauthorizedAccessException($"You do not have the required permission: {required}.");
            return (link.ClientId, link.Permissions);
        }

        if (links.Count > 1)
            throw new InvalidOperationException("Multiple client accounts found. Please specify a client ID.");

        throw new UnauthorizedAccessException("No client account found for this user.");
    }
}
