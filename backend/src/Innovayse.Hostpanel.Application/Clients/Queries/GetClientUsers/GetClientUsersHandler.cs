using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Queries.GetClientUsers;

/// <summary>Returns all users linked to a client (owner + additional non-owner users).</summary>
public sealed class GetClientUsersHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the people behind the linked subjects.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="identity">Reads the people behind the linked subjects.</param>
    public GetClientUsersHandler(IHostpanelDbContext context, IIdentityProvider identity)
    {
        _context = context;
        _identity = identity;
    }

    /// <summary>Returns all users linked to the client (owner first, then additional users).</summary>
    /// <param name="query">The query containing the client id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The client's users, or <c>CLIENT_NOT_FOUND</c> when the id names no client.</returns>
    public async Task<Result<IReadOnlyList<ClientUserDto>>> HandleAsync(GetClientUsersQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var client = await _context.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == query.ClientId, ct);
        if (client is null)
        {
            return Result<IReadOnlyList<ClientUserDto>>.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        var additionalUsers = await _context.ClientUsers.AsNoTracking()
            .Where(u => u.ClientId == query.ClientId)
            .ToListAsync(ct);

        // One lookup for everyone on the screen, rather than one per row.
        var subjects = new List<string> { client.UserId };
        subjects.AddRange(additionalUsers.Select(u => u.UserId));
        var accounts = await _identity.GetAccountsBySubjectsAsync(subjects, ct);

        var result = new List<ClientUserDto>();

        if (accounts.TryGetValue(client.UserId, out var owner))
        {
            result.Add(new ClientUserDto(
                owner.Subject, owner.FirstName, owner.LastName, owner.Email,
                IsOwner: true, (int)ClientPermission.All,
                owner.LastLoginAt,
                // When they became this client's owner, not when their account was created: the
                // account may predate the client and may belong to an external sign-on provider this product cannot ask.
                client.CreatedAt));
        }

        foreach (var link in additionalUsers)
        {
            if (!accounts.TryGetValue(link.UserId, out var account))
            {
                continue;
            }

            result.Add(new ClientUserDto(
                account.Subject, account.FirstName, account.LastName, account.Email,
                IsOwner: false, (int)link.Permissions,
                account.LastLoginAt, link.CreatedAt));
        }

        return Result<IReadOnlyList<ClientUserDto>>.Success(result);
    }
}
