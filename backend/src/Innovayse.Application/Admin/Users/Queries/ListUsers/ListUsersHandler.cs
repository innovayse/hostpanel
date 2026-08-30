namespace Innovayse.Application.Admin.Users.Queries.ListUsers;

using Innovayse.Application.Admin.Common;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="ListUsersQuery"/>: pages through the configured identity provider and
/// joins each row to its linked client account, if any.
/// </summary>
/// <param name="identity">Reads people from wherever they live.</param>
/// <param name="clientRepo">Client repository, for the accounts each person owns.</param>
public sealed class ListUsersHandler(IIdentityProvider identity, IClientRepository clientRepo)
{
    /// <summary>Returns one page of user summaries with the unpaged total.</summary>
    /// <param name="query">Search term and paging.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged list of user summaries.</returns>
    public async Task<PagedResult<UserListItemDto>> HandleAsync(ListUsersQuery query, CancellationToken ct)
    {
        var ps = Math.Clamp(query.PageSize, 1, 100);
        var pg = Math.Max(1, query.Page);

        var (accounts, total) = await identity.ListAsync(query.Search, pg, ps, ct);

        // One lookup for the page rather than one per row.
        var clientIds = await clientRepo.FindClientIdsByUserIdsAsync(
            accounts.Select(a => a.Subject).ToList(), ct);

        var items = accounts.Select(a => new UserListItemDto(
            a.Subject,
            clientIds.TryGetValue(a.Subject, out var clientId) ? clientId : null,
            a.FirstName, a.LastName, a.Email,
            // Language and the account's own creation date belong to whichever store holds
            // the person. A local deployment holds the language on its own account row, so it
            // is answered here; an SSO hands out neither, so both stay null. Left unanswered
            // rather than guessed at: a fabricated date on an admin screen is worse than a
            // blank one.
            a.Language, a.LastLoginAt, CreatedAt: default)).ToList();

        return new PagedResult<UserListItemDto>(items, total, pg, ps);
    }
}
