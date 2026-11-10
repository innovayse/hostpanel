using System.Globalization;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Queries.ListClients;

/// <summary>
/// Returns a paginated list of clients with the details of the people behind them. Supports
/// filtering by name (first, last or full name), company, client id, email, phone, status and client group. The order is
/// stable: last name, first name, then id, so a page never repeats or skips a row between requests.
/// </summary>
public sealed class ListClientsHandler
{
    /// <summary>How many address matches an email filter draws on. See the note at the call site.</summary>
    private const int EmailMatchLimit = 500;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the people behind the client rows.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="identity">Reads the people behind the client rows.</param>
    public ListClientsHandler(IHostpanelDbContext context, IIdentityProvider identity)
    {
        _context = context;
        _identity = identity;
    }

    /// <summary>Retrieves a paginated, filtered client list.</summary>
    /// <param name="query">The list query with pagination and filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The requested page of clients, never a refusal.</returns>
    public async Task<Result<PaginatedList<ClientListItemDto>>> HandleAsync(ListClientsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Page and page size are validated (400 when out of range), so they are used as given.

        // The address is not a column on the client row, so filtering by it means asking the
        // identity provider which subjects match and then filtering on those.
        //
        // Bounded, because the answer becomes an IN clause. A search matching more people than
        // this would build a query the database has to be talked into accepting, so the page is
        // drawn from the first EmailMatchLimit matches instead.
        IReadOnlyList<string>? emailUserIds = null;
        if (!string.IsNullOrWhiteSpace(query.Email))
        {
            var matches = await _identity.ListAsync(new IdentityAccountListQuery(query.Email, 1, EmailMatchLimit), ct);
            emailUserIds = matches.Items.Select(m => m.Subject).ToList();
        }

        var clients = _context.Clients.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            var pattern = LikePattern.Contains(term);

            // The whole-number term also finds the client by id. The id is parsed here, outside the
            // query, so the SQL compares a plain parameter.
            var searchId = int.TryParse(term, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedId) ? parsedId : (int?)null;

            // The full name is matched as "First Last" so a staff member can type a person's name the
            // way it is read, which neither column alone contains.
            clients = clients.Where(c =>
                c.Id == searchId ||
                EF.Functions.Like(c.FirstName.ToLower(), pattern, LikePattern.EscapeCharacter) ||
                EF.Functions.Like(c.LastName.ToLower(), pattern, LikePattern.EscapeCharacter) ||
                EF.Functions.Like((c.FirstName + " " + c.LastName).ToLower(), pattern, LikePattern.EscapeCharacter) ||
                (c.CompanyName != null && EF.Functions.Like(c.CompanyName.ToLower(), pattern, LikePattern.EscapeCharacter)));
        }

        if (!string.IsNullOrWhiteSpace(query.Phone))
        {
            var phonePattern = LikePattern.Contains(query.Phone.Trim());
            clients = clients.Where(c => c.Phone != null && EF.Functions.Like(c.Phone.ToLower(), phonePattern, LikePattern.EscapeCharacter));
        }

        if (query.Status is { } status)
        {
            clients = clients.Where(c => c.Status == status);
        }

        if (query.ClientGroupId is { } clientGroupId)
        {
            clients = clients.Where(c => c.ClientGroupId == clientGroupId);
        }

        if (emailUserIds is not null)
        {
            clients = clients.Where(c => emailUserIds.Contains(c.UserId));
        }

        var totalCount = await clients.CountAsync(ct);

        var items = await clients
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ThenBy(c => c.Id)
            .Paginate(query.Page, query.PageSize)
            .ToListAsync(ct);

        // One lookup for the whole page, rather than one per row.
        var accounts = await _identity.GetAccountsBySubjectsAsync(items.Select(c => c.UserId).Distinct(), ct);

        // One lookup for the groups of the page as well.
        var groupIds = items.Select(c => c.ClientGroupId).OfType<int>().Distinct().ToList();
        var groups = await _context.ClientGroups.AsNoTracking()
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, ct);

        var dtos = items.Select(c => MapToListItem(c, accounts, groups)).ToList();

        return Result<PaginatedList<ClientListItemDto>>.Success(new PaginatedList<ClientListItemDto>(dtos, totalCount, query.Page, query.PageSize));
    }

    /// <summary>Maps a <see cref="Client"/> to <see cref="ClientListItemDto"/>.</summary>
    /// <param name="client">The client to map.</param>
    /// <param name="accounts">Subject to account lookup for this page.</param>
    /// <param name="groups">Group id to group lookup for this page.</param>
    /// <returns>The list item DTO.</returns>
    private static ClientListItemDto MapToListItem(
        Client client, IReadOnlyDictionary<string, IdentityAccount> accounts, IReadOnlyDictionary<int, ClientGroup> groups)
    {
        // A client row whose subject resolves to nobody is flagged rather than hidden: it is how a
        // legacy or deleted account shows up, and the screen has a column for it.
        var account = accounts.GetValueOrDefault(client.UserId);
        var group = client.ClientGroupId is { } groupId ? groups.GetValueOrDefault(groupId) : null;

        return new ClientListItemDto(
            client.Id, client.UserId, account?.Email ?? string.Empty,
            client.FirstName, client.LastName, client.CompanyName, client.Status,
            account is null, account?.TwoFactorEnabled ?? false, client.CreatedAt,
            group?.Id, group?.Name, group?.Color);
    }
}
