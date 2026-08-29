namespace Innovayse.Application.Clients.Queries.GetClientUsers;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="GetClientUsersQuery"/>.
/// Returns all users linked to a client (owner + additional non-owner users).
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="clientUserRepo">Client-user link repository.</param>
/// <param name="identity">Reads the people behind the linked subjects.</param>
public sealed class GetClientUsersHandler(
    IClientRepository clientRepo,
    IClientUserRepository clientUserRepo,
    IIdentityProvider identity)
{
    /// <summary>
    /// Returns all users linked to the client (owner first, then additional users).
    /// </summary>
    /// <param name="query">The query containing the client ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Read-only list of client user DTOs.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the client is not found.</exception>
    public async Task<IReadOnlyList<ClientUserDto>> HandleAsync(GetClientUsersQuery query, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(query.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {query.ClientId} not found.");

        var additionalUsers = await clientUserRepo.FindByClientIdAsync(query.ClientId, ct);

        // One lookup for everyone on the screen, rather than one per row. The previous
        // version asked per user, which is a round trip per row once the people behind
        // those subjects live in another service.
        var subjects = new List<string> { client.UserId };
        subjects.AddRange(additionalUsers.Select(u => u.UserId));
        var accounts = await identity.GetAccountsBySubjectsAsync(subjects, ct);

        var result = new List<ClientUserDto>();

        if (accounts.TryGetValue(client.UserId, out var owner))
        {
            result.Add(new ClientUserDto(
                owner.Subject, owner.FirstName, owner.LastName, owner.Email,
                IsOwner: true, (int)ClientPermission.All,
                owner.LastLoginAt,
                // When they became this client's owner, not when their account was created.
                // The account may predate the client, may belong to an SSO that this
                // product cannot ask, and the column beside it already means the same
                // thing for every other row on the screen.
                client.CreatedAt));
        }

        foreach (var link in additionalUsers)
        {
            if (!accounts.TryGetValue(link.UserId, out var account)) continue;

            result.Add(new ClientUserDto(
                account.Subject, account.FirstName, account.LastName, account.Email,
                IsOwner: false, (int)link.Permissions,
                account.LastLoginAt, link.CreatedAt));
        }

        return result;
    }
}
