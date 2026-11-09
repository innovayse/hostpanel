using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Application.Reports.Extensions;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetServicesReport;

/// <summary>Handles <see cref="GetServicesReportQuery"/>.</summary>
public sealed class GetServicesReportHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The bus the owning clients' and the products' names are asked through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The bus the owning clients' and the products' names are asked through.</param>
    public GetServicesReportHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The filters and page to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching page of services and the unfiltered total. Never refuses.</returns>
    /// <exception cref="InvalidOperationException">A service names a client or a product that does not exist, so the database is broken.</exception>
    public async Task<Result<PaginatedList<ServiceReportDto>>> HandleAsync(GetServicesReportQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var services = _context.ClientServices.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            services = services.Where(s => s.Status.ToString() == query.Status);
        }

        if (query.BillingCycle.HasValue)
        {
            services = services.Where(s => s.BillingCycle == query.BillingCycle.Value);
        }

        if (query.CreatedFrom.HasValue)
        {
            services = services.Where(s => s.CreatedAt >= query.CreatedFrom.Value.StartOfDayUtc());
        }

        if (query.CreatedTo.HasValue)
        {
            services = services.Where(s => s.CreatedAt < query.CreatedTo.Value.EndOfDayExclusiveUtc());
        }

        if (query.NextDueFrom.HasValue)
        {
            services = services.Where(s => s.NextRenewalAt >= query.NextDueFrom.Value.StartOfDayUtc());
        }

        if (query.NextDueTo.HasValue)
        {
            services = services.Where(s => s.NextRenewalAt < query.NextDueTo.Value.EndOfDayExclusiveUtc());
        }

        if (query.TerminatedFrom.HasValue)
        {
            services = services.Where(s => s.TerminatedAt >= query.TerminatedFrom.Value.StartOfDayUtc());
        }

        if (query.TerminatedTo.HasValue)
        {
            services = services.Where(s => s.TerminatedAt < query.TerminatedTo.Value.EndOfDayExclusiveUtc());
        }

        var totalCount = await services.CountAsync(ct);

        var items = await services
            .OrderByDescending(s => s.CreatedAt)
            .Paginate(query.Page, query.PageSize)
            .ToListAsync(ct);

        // FK_ClientServices_Clients_ClientId and FK_ClientServices_Products_ProductId restrict
        // deleting a client or a product that still has services, so every one named on the page is there.
        var clientIds = items.Select(s => s.ClientId).Distinct().ToList();
        var clients = await _bus.GetRequiredClientBriefsByIdAsync(clientIds, ct);
        var productIds = items.Select(s => s.ProductId).Distinct().ToList();
        var products = await _bus.GetRequiredProductSummariesByIdAsync(productIds, ct);

        var result = items.Select(s => new ServiceReportDto(
            s.Id,
            s.ClientId,
            clients[s.ClientId].FullName,
            s.ProductId,
            products[s.ProductId].Name,
            s.Domain,
            s.BillingCycle,
            s.FirstPaymentAmount,
            s.RecurringAmount,
            s.PaymentMethod,
            s.CreatedAt.ToString("yyyy-MM-dd"),
            s.NextRenewalAt?.ToString("yyyy-MM-dd"),
            s.TerminatedAt?.ToString("yyyy-MM-dd"),
            s.Status,
            s.AdminNotes)).ToList();

        return Result<PaginatedList<ServiceReportDto>>.Success(new PaginatedList<ServiceReportDto>(result, totalCount, query.Page, query.PageSize));
    }
}
