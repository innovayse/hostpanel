using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetServiceChangeRequests;

/// <summary>Handles listing plan change requests with the client and product names an admin list shows.</summary>
public sealed class GetServiceChangeRequestsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The bus the product and client queries are dispatched through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The bus the product and client queries are dispatched through.</param>
    public GetServiceChangeRequestsHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the query. Enriches each request with the product names and the client full name.</summary>
    /// <param name="qry">The filters, the page and the page size.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, always: an out-of-range page answers an empty page, not a refusal.</returns>
    /// <exception cref="InvalidOperationException">A request names a product or a client that does not exist, so the database is broken.</exception>
    public async Task<Result<PaginatedList<ServiceChangeRequestDto>>> HandleAsync(GetServiceChangeRequestsQuery qry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(qry);

        var requests = _context.ServiceChangeRequests.AsNoTracking().AsQueryable();
        if (qry.ServiceId is { } serviceId)
        {
            requests = requests.Where(r => r.ServiceId == serviceId);
        }

        if (qry.Status is { } status)
        {
            requests = requests.Where(r => r.Status == status);
        }

        var total = await requests.CountAsync(ct);
        var page = await requests
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Paginate(qry.Page, qry.PageSize)
            .ToListAsync(ct);

        // Products and clients belong to other modules: the page's distinct ids are asked through their queries once.
        // The foreign keys of the request restrict deleting a product or a client it names, so every one is there.
        var productIds = page.SelectMany(r => new[] { r.FromProductId, r.ToProductId }).Distinct().ToList();
        var products = await _bus.GetRequiredProductSummariesByIdAsync(productIds, ct);
        var clients = await _bus.GetRequiredClientBriefsByIdAsync(page.Select(r => r.ClientId).Distinct().ToList(), ct);

        var dtos = page
            .Select(r => new ServiceChangeRequestDto(
                r.Id,
                r.ServiceId,
                r.ClientId,
                clients[r.ClientId].FullName,
                r.FromProductId,
                products[r.FromProductId].Name,
                r.ToProductId,
                products[r.ToProductId].Name,
                r.Type,
                r.Status,
                r.ProrationAmount,
                r.CurrencyCode,
                r.NewRecurringAmount,
                r.InvoiceId,
                r.Note,
                r.CreatedAt,
                r.ResolvedAt))
            .ToList();

        return Result<PaginatedList<ServiceChangeRequestDto>>.Success(
            new PaginatedList<ServiceChangeRequestDto>(dtos, total, qry.Page, qry.PageSize));
    }
}
