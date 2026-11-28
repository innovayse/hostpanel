using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Services.Extensions;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Queries.ListCancellationRequests;

/// <summary>
/// Handles listing cancellation requests with enriched service and client data, narrowed by the search text on the
/// reason, id and the service it is for (its domain, product and client), the type and the status in SQL ahead of
/// paging.
/// </summary>
public sealed class ListCancellationRequestsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The bus the service search, product and client queries are dispatched through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The bus the service search, product and client queries are dispatched through.</param>
    public ListCancellationRequestsHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>
    /// Handles the query. Enriches each request with the product name and client full name.
    /// </summary>
    /// <param name="qry">The search, the type and status filters and the paging window.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, always: an out-of-range page answers an empty page, not a refusal.</returns>
    /// <exception cref="InvalidOperationException">
    /// A service names a product or a client that does not exist, so the database is broken.
    /// </exception>
    public async Task<Result<PaginatedList<CancellationRequestDto>>> HandleAsync(
        ListCancellationRequestsQuery qry, CancellationToken ct)
    {
        // The search, the filters and the count run on the requests alone, ahead of the join: the join below only adds
        // the service's product and client, and it is an inner join over a cascading key, so it never drops a request.
        // The service a request is for is matched through the lookup the services lists share (domain, product,
        // client, id).
        var matchingServiceIds = await _bus.FindServiceIdsMatchingAsync(qry.Search, ct);
        var requests = _context.CancellationRequests.AsNoTracking()
            .SearchIncludingIdOrMatching(
                qry.Search, r => r.Id, r => matchingServiceIds.Contains(r.ServiceId), r => r.Reason);
        if (qry.Type is { } type)
        {
            requests = requests.Where(r => r.Type == type);
        }

        if (qry.Status is { } status)
        {
            requests = requests.Where(r => r.Status == status);
        }

        var total = await requests.CountAsync(ct);

        // An inner join: FK_CancellationRequests_ClientServices_ServiceId cascades, so a request
        // never outlives its service. The id ends the order so a page never repeats or skips a row between requests.
        var rows = await (
                from r in requests
                join s in _context.ClientServices.AsNoTracking() on r.ServiceId equals s.Id
                orderby r.CreatedAt descending, r.Id descending
                select new
                {
                    Request = r,
                    s.ProductId,
                    s.ClientId,
                })
            .Paginate(qry.Page, qry.PageSize)
            .ToListAsync(ct);

        // Products and clients belong to other modules: the page's distinct ids are asked through their queries once.
        // FK_ClientServices_Products_ProductId restricts deleting a product that has services, so every product is
        // there.
        var productIds = rows.Select(x => x.ProductId).Distinct().ToList();
        var products = await _bus.GetRequiredProductSummariesByIdAsync(productIds, ct);

        // FK_ClientServices_Clients_ClientId restricts deleting a client that has services, so every owner is there.
        var clientIds = rows.Select(x => x.ClientId).Distinct().ToList();
        var clients = await _bus.GetRequiredClientBriefsByIdAsync(clientIds, ct);

        var dtos = rows
            .Select(x => new CancellationRequestDto(
                x.Request.Id,
                x.Request.ServiceId,
                products[x.ProductId].Name,
                x.ClientId,
                clients[x.ClientId].FullName,
                x.Request.Type,
                x.Request.Reason,
                x.Request.Status,
                x.Request.CreatedAt))
            .ToList();

        return Result<PaginatedList<CancellationRequestDto>>.Success(
            new PaginatedList<CancellationRequestDto>(dtos, total, qry.Page, qry.PageSize));
    }
}
