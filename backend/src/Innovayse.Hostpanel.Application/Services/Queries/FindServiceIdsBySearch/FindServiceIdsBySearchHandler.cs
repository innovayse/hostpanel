using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Queries.FindServiceIdsByDomainName;
using Innovayse.Hostpanel.Application.Products.Queries.FindProductIdsByName;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Queries.FindServiceIdsBySearch;

/// <summary>
/// Handles <see cref="FindServiceIdsBySearchQuery"/>: a service shows its registered domain when one is linked and its
/// own domain otherwise, and neither the product name nor the client is a column of the service, so the owners of those
/// tables are asked which ids match. Each answer is one array parameter in the query, however many ids it holds.
/// </summary>
public sealed class FindServiceIdsBySearchHandler
{
    /// <summary>The module's data context the services are read from.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The bus the domain, product and client lookups are dispatched through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context the services are read from.</param>
    /// <param name="bus">The bus the domain, product and client lookups are dispatched through.</param>
    public FindServiceIdsBySearchHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The text to look for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ids of the matching services, in no particular order. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<int>>> HandleAsync(FindServiceIdsBySearchQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Search))
        {
            return Result<IReadOnlyList<int>>.Success([]);
        }

        var domainServiceIds = await FindIdsAsync(new FindServiceIdsByDomainNameQuery(query.Search), ct);
        var productIds = await FindIdsAsync(new FindProductIdsByNameQuery(query.Search), ct);
        var clientIds = await _bus.FindClientIdsMatchingAsync(query.Search, ct);

        IReadOnlyList<int> serviceIds = await _context.ClientServices
            .AsNoTracking()
            .SearchIncludingIdOrMatching(
                query.Search,
                s => s.Id,
                s => domainServiceIds.Contains(s.Id) || productIds.Contains(s.ProductId) || clientIds.Contains(s.ClientId),
                s => s.Domain)
            .Select(s => s.Id)
            .ToListAsync(ct);

        return Result<IReadOnlyList<int>>.Success(serviceIds);
    }

    /// <summary>Asks another module for the ids a lookup matches.</summary>
    /// <param name="query">The lookup, a message answered with the matching ids.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching ids.</returns>
    private async Task<IReadOnlyList<int>> FindIdsAsync(object query, CancellationToken ct)
    {
        var answer = await _bus.InvokeAsync<Result<IReadOnlyList<int>>>(query, ct);

        return answer.Value ?? [];
    }
}
