namespace Innovayse.Application.Clients.Queries.GetClientUsers;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Clients.DTOs;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="GetClientUsersQuery"/>.
/// Returns all users linked to a client (owner + additional non-owner users).
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="clientUserRepo">Client-user link repository.</param>
/// <param name="userService">Identity user service.</param>
public sealed class GetClientUsersHandler(
    IClientRepository clientRepo,
    IClientUserRepository clientUserRepo,
    IUserService userService)
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

        var result = new List<ClientUserDto>();

        // Owner — use GetUserWithAccountsAsync which returns full profile data
        var ownerDetail = await userService.GetUserWithAccountsAsync(client.UserId, ct);
        if (ownerDetail is not null)
        {
            result.Add(new ClientUserDto(
                ownerDetail.Id, ownerDetail.FirstName, ownerDetail.LastName,
                ownerDetail.Email, true, (int)ClientPermission.All,
                ownerDetail.LastLoginAt, ownerDetail.CreatedAt));
        }

        // Additional non-owner users
        foreach (var cu in additionalUsers)
        {
            var userDetail = await userService.GetUserWithAccountsAsync(cu.UserId, ct);
            if (userDetail is not null)
            {
                result.Add(new ClientUserDto(
                    userDetail.Id, userDetail.FirstName, userDetail.LastName,
                    userDetail.Email, false, (int)cu.Permissions,
                    userDetail.LastLoginAt, cu.CreatedAt));
            }
        }

        return result;
    }
}
