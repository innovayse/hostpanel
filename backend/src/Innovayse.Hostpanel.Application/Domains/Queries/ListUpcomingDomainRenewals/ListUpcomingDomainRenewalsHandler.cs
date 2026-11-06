using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Domains.Queries.ListUpcomingDomainRenewals;

/// <summary>Handles <see cref="ListUpcomingDomainRenewalsQuery"/>: one page of the active domains expiring inside the window.</summary>
public sealed class ListUpcomingDomainRenewalsHandler
{
    /// <summary>The data context the page is read from.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, used to name each domain's client through the Clients module.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock the window starts from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context the page is read from.</param>
    /// <param name="bus">Wolverine bus, used to name each domain's client through the Clients module.</param>
    /// <param name="clock">The clock the window starts from.</param>
    public ListUpcomingDomainRenewalsHandler(IHostpanelDbContext context, IMessageBus bus, IClock clock)
    {
        _context = context;
        _bus = bus;
        _clock = clock;
    }

    /// <summary>Lists the page.</summary>
    /// <param name="query">The window and the page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, always: an out-of-range page answers an empty page.</returns>
    public async Task<Result<PaginatedList<DomainRegistrationDto>>> HandleAsync(ListUpcomingDomainRenewalsQuery query, CancellationToken ct)
    {
        var until = _clock.UtcNow.AddDays((int)query.Window);
        var due = _context.Domains.AsNoTracking()
            .Where(d => d.Status == DomainStatus.Active && d.ExpiresAt <= until);

        var total = await due.CountAsync(ct);
        var domains = await due
            .OrderBy(d => d.ExpiresAt).ThenBy(d => d.Id)
            .Paginate(query.Page, query.PageSize)
            .ToListAsync(ct);

        // FK_Domains_Clients_ClientId restricts deleting a client that has domains, so every owner is there.
        var clients = await _bus.GetRequiredClientBriefsByIdAsync(domains.Select(d => d.ClientId).Distinct().ToList(), ct);

        var items = domains
            .Select(d => new DomainRegistrationDto(
                d.Id, d.ClientId, clients[d.ClientId].FullName, d.Name, d.RegistrationPeriod, d.Registrar,
                d.RecurringAmount, d.PriceCurrency, d.NextDueDate, d.ExpiresAt, d.Status, d.AutoRenew))
            .ToList();

        return Result.Success(new PaginatedList<DomainRegistrationDto>(items, total, query.Page, query.PageSize));
    }
}
