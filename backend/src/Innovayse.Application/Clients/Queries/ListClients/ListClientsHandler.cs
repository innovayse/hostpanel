namespace Innovayse.Application.Clients.Queries.ListClients;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Clients.DTOs;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="ListClientsQuery"/>.
/// Returns a paginated list of clients with email addresses from Identity.
/// Supports filtering by name, email, phone, and status.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="userService">User service for email lookups.</param>
public sealed class ListClientsHandler(IClientRepository clientRepo, IUserService userService)
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

        // Email lives on Identity user — resolve matching user IDs first
        IEnumerable<string>? emailUserIds = null;
        if (!string.IsNullOrWhiteSpace(query.Email))
        {
            emailUserIds = await userService.FindUserIdsByEmailAsync(query.Email, ct);
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

        // Batch-fetch emails for the returned page
        var userIds = items.Select(c => c.UserId).Distinct();
        var emails = await userService.GetEmailsByIdsAsync(userIds, ct);

        var dtos = items.Select(c => MapToListItem(c, emails)).ToList();

        return new PagedResult<ClientListItemDto>(dtos, totalCount, page, pageSize);
    }

    /// <summary>Maps a <see cref="Client"/> to <see cref="ClientListItemDto"/>.</summary>
    /// <param name="client">The client to map.</param>
    /// <param name="emails">User ID to email lookup.</param>
    /// <returns>The list item DTO.</returns>
    private static ClientListItemDto MapToListItem(Client client, Dictionary<string, string> emails) =>
        new(client.Id, client.UserId, emails.GetValueOrDefault(client.UserId, ""),
            client.FirstName, client.LastName, client.CompanyName, client.Status,
            !emails.ContainsKey(client.UserId), client.CreatedAt);
}
