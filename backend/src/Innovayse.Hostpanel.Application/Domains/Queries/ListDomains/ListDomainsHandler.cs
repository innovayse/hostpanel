using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Domains.Queries.ListDomains;

/// <summary>Returns a paginated list of all domains for the admin view.</summary>
public sealed class ListDomainsHandler
{
    /// <summary>The data context the page is read from.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, used to name each domain's client through the Clients module.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context the page is read from.</param>
    /// <param name="bus">Wolverine bus, used to name each domain's client through the Clients module.</param>
    public ListDomainsHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>
    /// Handles <see cref="ListDomainsQuery"/>.
    /// </summary>
    /// <param name="query">The list domains query with pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paged result containing domain registration items with client names. Never refuses.</returns>
    public async Task<Result<PaginatedList<DomainRegistrationDto>>> HandleAsync(
        ListDomainsQuery query, CancellationToken ct)
    {
        var domainsQuery = _context.Domains.AsNoTracking();
        if (query.ClientId.HasValue)
        {
            domainsQuery = domainsQuery.Where(x => x.ClientId == query.ClientId.Value);
        }

        var orderedDomains = domainsQuery.OrderBy(x => x.Name);
        var totalCount = await orderedDomains.CountAsync(ct);
        var items = await orderedDomains
            .Paginate(query.Page, query.PageSize)
            .ToListAsync(ct);

        // One lookup for the whole page rather than one per row; FK_Domains_Clients_ClientId
        // restricts deleting a client that has domains, so every owner is there.
        var clientIds = items.Select(d => d.ClientId).Distinct().ToList();
        var clients = await _bus.GetRequiredClientBriefsByIdAsync(clientIds, ct);

        var dtos = items
            .Select(d => new DomainRegistrationDto(
                d.Id,
                d.ClientId,
                clients[d.ClientId].FullName,
                d.Name,
                d.RegistrationPeriod,
                d.Registrar,
                d.RecurringAmount,
                d.PriceCurrency,
                d.NextDueDate,
                d.ExpiresAt,
                d.Status,
                d.AutoRenew))
            .ToList();

        var page = new PaginatedList<DomainRegistrationDto>(dtos, totalCount, query.Page, query.PageSize);
        return Result.Success(page);
    }
}
