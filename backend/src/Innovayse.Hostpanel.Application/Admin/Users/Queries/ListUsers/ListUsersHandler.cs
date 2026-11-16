using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Admin.Users.Queries.ListUsers;

/// <summary>
/// Handles <see cref="ListUsersQuery"/>: pages through the configured identity provider and
/// joins each row to its linked client account, if any.
/// </summary>
public sealed class ListUsersHandler
{
    /// <summary>Reads people from wherever they live.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Wolverine bus, used to find the client account a person owns through the Clients module.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="identity">Reads people from wherever they live.</param>
    /// <param name="bus">Wolverine bus, used to find the client account a person owns through the Clients module.</param>
    public ListUsersHandler(IIdentityProvider identity, IMessageBus bus)
    {
        _identity = identity;
        _bus = bus;
    }

    /// <summary>Returns one page of user summaries with the unpaged total.</summary>
    /// <param name="query">Search text and paging.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the page. This use case refuses nothing.</returns>
    public async Task<Result<PaginatedList<UserListItemDto>>> HandleAsync(ListUsersQuery query, CancellationToken ct)
    {
        var accounts = await _identity.ListAsync(new IdentityAccountListQuery(query.Search, query.Page, query.PageSize), ct);

        // One lookup for the whole page rather than one per row.
        var subjects = accounts.Items.Select(a => a.Subject).ToList();
        var clients = await _bus.GetClientBriefsBySubjectAsync(subjects, ct);

        var items = accounts.Items.Select(a => new UserListItemDto(
            a.Subject,
            clients.TryGetValue(a.Subject, out var client) ? client.Id : null,
            a.FirstName, a.LastName, a.Email,
            // Language and the account's own creation date belong to whichever store holds
            // the person. A local deployment holds the language on its own account row, so it
            // is answered here; an external sign-on provider hands out neither, so both stay null. Left unanswered
            // rather than guessed at: a fabricated date on an admin screen is worse than a
            // blank one.
            a.Language, a.LastLoginAt, CreatedAt: default)).ToList();

        var page = new PaginatedList<UserListItemDto>(items, accounts.TotalCount, query.Page, query.PageSize);
        return Result<PaginatedList<UserListItemDto>>.Success(page);
    }
}
