using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Admin.Queries.GetRecentActivity;

/// <summary>
/// Handles <see cref="GetRecentActivityQuery"/> by pulling the newest client registration,
/// invoice-overdue, payment, opened-ticket and domain-expiry events from the data context and merging
/// them by time. Each event carries its kind and the facts about it (client, amount, subject), never a sentence.
/// </summary>
public sealed class GetRecentActivityHandler
{
    /// <summary>How far back a client registration, a payment or an opened ticket is still worth surfacing.</summary>
    private static readonly TimeSpan ClientLookback = TimeSpan.FromDays(14);

    /// <summary>How far ahead a domain expiry is worth surfacing.</summary>
    private static readonly TimeSpan DomainLookahead = TimeSpan.FromDays(30);

    /// <summary>The default and the ceiling for how many events a caller may ask for.</summary>
    private const int DefaultLimit = 10;

    /// <summary>The data context, for recently registered clients, invoices, tickets and domains nearing expiry.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>The message bus, for the Clients module's client briefs that name the client of each event.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The caller, whose read marks decide which events are read.</summary>
    private readonly ICurrentUserService _currentUser;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context, for recently registered clients, invoices, tickets and domains nearing expiry.</param>
    /// <param name="clock">The source of the current moment.</param>
    /// <param name="bus">The message bus, for the Clients module's client briefs that name the client of each event.</param>
    /// <param name="currentUser">The caller, whose read marks decide which events are read.</param>
    public GetRecentActivityHandler(IHostpanelDbContext context, IClock clock, IMessageBus bus, ICurrentUserService currentUser)
    {
        _context = context;
        _clock = clock;
        _bus = bus;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Builds the merged, newest-first activity feed.
    /// </summary>
    /// <param name="query">The query request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with up to the requested number of events, newest first. A limit outside 1-50
    /// substitutes the default of 10 rather than being refused, matching this feed's previous
    /// behaviour. This use case refuses nothing.
    /// </returns>
    public async Task<Result<IReadOnlyList<ActivityEventDto>>> HandleAsync(
        GetRecentActivityQuery query, CancellationToken ct)
    {
        var limit = query.Limit is > 0 and <= GetRecentActivityQuery.MaxLimit ? query.Limit : DefaultLimit;
        var now = _clock.UtcNow;

        var lookbackStart = now - ClientLookback;
        var recentClients = await _context.Clients
            .AsNoTracking()
            .Where(c => c.CreatedAt >= lookbackStart && c.CreatedAt <= now)
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
        var overdueInvoices = await _context.Invoices
            .AsNoTracking()
            .Where(x => x.Status == InvoiceStatus.Overdue)
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
        var paidInvoices = await _context.Invoices
            .AsNoTracking()
            .Where(x => Invoice.PaidStatuses.Contains(x.Status) && x.PaidAt >= lookbackStart && x.PaidAt <= now)
            .OrderByDescending(x => x.PaidAt)
            .Take(limit)
            .ToListAsync(ct);
        var openedTickets = await _context.Tickets
            .AsNoTracking()
            .Where(x => x.Status == TicketStatus.Open && x.CreatedAt >= lookbackStart && x.CreatedAt <= now)
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
        var expiryThreshold = now + DomainLookahead;
        var expiringDomains = await _context.Domains
            .IncludeChildren()
            .Where(x => x.ExpiresAt < expiryThreshold && x.Status == DomainStatus.Active)
            .ToListAsync(ct);

        var clientIds = recentClients.Select(c => c.Id)
            .Concat(overdueInvoices.Select(i => i.ClientId))
            .Concat(paidInvoices.Select(i => i.ClientId))
            .Concat(openedTickets.Select(t => t.ClientId))
            .Concat(expiringDomains.Select(d => d.ClientId))
            .Distinct()
            .ToList();
        var clients = await _bus.GetRequiredClientBriefsByIdAsync(clientIds, ct);

        var events = new List<ActivityEventDto>(
            recentClients.Count + overdueInvoices.Count + paidInvoices.Count + openedTickets.Count + expiringDomains.Count);

        events.AddRange(recentClients.Select(c => new ActivityEventDto(
            ActivityEventType.ClientRegistered,
            c.CreatedAt,
            c.Id,
            c.Id,
            clients[c.Id].FullName,
            null,
            null,
            null,
            false)));

        events.AddRange(overdueInvoices.Select(i => new ActivityEventDto(
            ActivityEventType.InvoiceOverdue,
            i.DueDate,
            i.Id,
            i.ClientId,
            clients[i.ClientId].FullName,
            null,
            i.Total,
            i.Currency,
            false)));

        // The filter above keeps only invoices that have a PaidAt, so the value is always present here.
        events.AddRange(paidInvoices.Select(i => new ActivityEventDto(
            ActivityEventType.PaymentReceived,
            i.PaidAt!.Value,
            i.Id,
            i.ClientId,
            clients[i.ClientId].FullName,
            null,
            i.Total,
            i.Currency,
            false)));

        events.AddRange(openedTickets.Select(t => new ActivityEventDto(
            ActivityEventType.TicketOpened,
            t.CreatedAt,
            t.Id,
            t.ClientId,
            clients[t.ClientId].FullName,
            t.Subject,
            null,
            null,
            false)));

        events.AddRange(expiringDomains.Select(d => new ActivityEventDto(
            ActivityEventType.DomainExpiring,
            d.ExpiresAt,
            d.Id,
            d.ClientId,
            clients[d.ClientId].FullName,
            d.Name,
            null,
            null,
            false)));

        var newest = events
            .OrderByDescending(e => e.OccurredAt)
            .Take(limit)
            .ToList();

        var userId = _currentUser.UserId;
        var newestKeys = newest.Select(e => e.Key).ToList();
        var readKeys = userId is null
            ? new HashSet<string>()
            : (await _context.ActivityReads
                .AsNoTracking()
                .Where(r => r.UserId == userId && newestKeys.Contains(r.EventKey))
                .Select(r => r.EventKey)
                .ToListAsync(ct)).ToHashSet();

        IReadOnlyList<ActivityEventDto> merged = newest
            .Select(e => e with { IsRead = readKeys.Contains(e.Key) })
            .ToList();

        return Result<IReadOnlyList<ActivityEventDto>>.Success(merged);
    }
}
