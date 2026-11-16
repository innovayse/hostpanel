using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetDomainsReport;

/// <summary>Handles <see cref="GetDomainsReportQuery"/>.</summary>
public sealed class GetDomainsReportHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The bus the owning clients' names are asked through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The bus the owning clients' names are asked through.</param>
    public GetDomainsReportHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The filters and page to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching page of domains and the unfiltered total. Never refuses.</returns>
    /// <exception cref="InvalidOperationException">A domain names a client that does not exist, so the database is broken.</exception>
    public async Task<Result<PaginatedList<DomainReportDto>>> HandleAsync(GetDomainsReportQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var domains = _context.Domains.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            domains = domains.Where(d => d.Status.ToString() == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.Registrar))
        {
            domains = domains.Where(d => d.Registrar != null && d.Registrar.ToLower().Contains(query.Registrar.ToLower()));
        }

        if (query.RegisteredFrom.HasValue)
        {
            domains = domains.Where(d => d.RegisteredAt >= query.RegisteredFrom.Value.StartOfDayUtc());
        }

        if (query.RegisteredTo.HasValue)
        {
            domains = domains.Where(d => d.RegisteredAt < query.RegisteredTo.Value.EndOfDayExclusiveUtc());
        }

        if (query.ExpiresFrom.HasValue)
        {
            domains = domains.Where(d => d.ExpiresAt >= query.ExpiresFrom.Value.StartOfDayUtc());
        }

        if (query.ExpiresTo.HasValue)
        {
            domains = domains.Where(d => d.ExpiresAt < query.ExpiresTo.Value.EndOfDayExclusiveUtc());
        }

        if (query.NextDueFrom.HasValue)
        {
            domains = domains.Where(d => d.NextDueDate >= query.NextDueFrom.Value.StartOfDayUtc());
        }

        if (query.NextDueTo.HasValue)
        {
            domains = domains.Where(d => d.NextDueDate < query.NextDueTo.Value.EndOfDayExclusiveUtc());
        }

        var totalCount = await domains.CountAsync(ct);

        var items = await domains
            .OrderByDescending(d => d.RegisteredAt)
            .Paginate(query.Page, query.PageSize)
            .Select(d => new
            {
                d.Id,
                d.ClientId,
                d.OrderId,
                d.OrderType,
                d.Name,
                d.FirstPaymentAmount,
                d.RecurringAmount,
                d.RegistrationPeriod,
                d.RegisteredAt,
                d.ExpiresAt,
                d.NextDueDate,
                d.Registrar,
                d.PaymentMethod,
                d.Status,
                d.AdminNotes,
            })
            .ToListAsync(ct);

        // FK_Domains_Clients_ClientId restricts deleting a client that has domains, so every owner on
        // the page is there; the name comes from the client record, never from the identity store.
        var clientIds = items.Select(d => d.ClientId).Distinct().ToList();
        var clients = await _bus.GetRequiredClientBriefsByIdAsync(clientIds, ct);

        var result = items.Select(d => new DomainReportDto(
            d.Id,
            d.ClientId,
            clients[d.ClientId].FullName,
            d.OrderId,
            d.OrderType,
            d.Name,
            new DomainReportAmountsDto(d.FirstPaymentAmount, d.RecurringAmount),
            d.RegistrationPeriod,
            new DomainReportDatesDto(
                d.RegisteredAt.ToString("yyyy-MM-dd"),
                d.ExpiresAt.ToString("yyyy-MM-dd"),
                d.NextDueDate.ToString("yyyy-MM-dd")),
            d.Registrar,
            d.PaymentMethod,
            d.Status,
            d.AdminNotes)).ToList();

        return Result<PaginatedList<DomainReportDto>>.Success(new PaginatedList<DomainReportDto>(result, totalCount, query.Page, query.PageSize));
    }
}
