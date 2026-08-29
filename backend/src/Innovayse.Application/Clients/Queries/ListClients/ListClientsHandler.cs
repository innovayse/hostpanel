namespace Innovayse.Application.Clients.Queries.ListClients;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="ListClientsQuery"/>.
/// Returns a paginated list of clients with the details of the people behind them.
/// Supports filtering by name, email, phone, and status.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="identity">Reads the people behind the client rows.</param>
public sealed class ListClientsHandler(IClientRepository clientRepo, IIdentityProvider identity)
{
    /// <summary>
    /// Retrieves a paginated, filtered client list.
    /// </summary>
    /// <param name="query">The list query with pagination and filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged result of client summary DTOs.</returns>
    public async Task<PagedResult<ClientListItemDto>> HandleAsync(ListClientsQuery query, CancellationToken ct)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var page = Math.Max(1, query.Page);

        // The address is not a column on the client row, so filtering by it means asking
        // the identity provider which subjects match and then filtering on those.
        //
        // Bounded, because the answer becomes an IN clause. A search matching more people
        // than this would build a query the database has to be talked into accepting, so
        // the page is drawn from the first EmailMatchLimit matches instead. Narrow the
        // term to see the rest — which is what a search box is for.
        IEnumerable<string>? emailUserIds = null;
        if (!string.IsNullOrWhiteSpace(query.Email))
        {
            var (matches, _) = await identity.ListAsync(query.Email, page: 1, EmailMatchLimit, ct);
            emailUserIds = matches.Select(m => m.Subject).ToList();
        }

        // Parse status filter
        ClientStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<ClientStatus>(query.Status, true, out var parsed))
        {
            statusFilter = parsed;
        }

        var (items, totalCount) = await clientRepo.ListAsync(
            page, pageSize, query.Search,
            query.Phone, statusFilter, emailUserIds, ct);

        // One lookup for the whole page. It used to take two — one for addresses, one for
        // second-factor state — which was two round trips for the same set of subjects.
        var accounts = await identity.GetAccountsBySubjectsAsync(
            items.Select(c => c.UserId).Distinct(), ct);

        var dtos = items.Select(c => MapToListItem(c, accounts)).ToList();

        return new PagedResult<ClientListItemDto>(dtos, totalCount, page, pageSize);
    }

    /// <summary>
    /// How many address matches an email filter draws on. See the note at the call site.
    /// </summary>
    private const int EmailMatchLimit = 500;

    /// <summary>Maps a <see cref="Client"/> to <see cref="ClientListItemDto"/>.</summary>
    /// <param name="client">The client to map.</param>
    /// <param name="accounts">Subject to account lookup for this page.</param>
    /// <returns>The list item DTO.</returns>
    private static ClientListItemDto MapToListItem(
        Client client, IReadOnlyDictionary<string, IdentityAccount> accounts)
    {
        // A client row whose subject resolves to nobody is flagged rather than hidden: it
        // is how a legacy or deleted account shows up, and the screen has a column for it.
        var account = accounts.GetValueOrDefault(client.UserId);

        return new ClientListItemDto(
            client.Id, client.UserId, account?.Email ?? string.Empty,
            client.FirstName, client.LastName, client.CompanyName, client.Status,
            account is null, account?.TwoFactorEnabled ?? false, client.CreatedAt);
    }
}
