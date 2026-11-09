using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Queries.GetRecentActivity;

/// <summary>
/// Handles <see cref="GetRecentActivityQuery"/> by pulling the newest client registration,
/// invoice-overdue and domain-expiry events from the data context and merging them by time.
/// </summary>
public sealed class GetRecentActivityHandler
{
    /// <summary>How far back a client registration is still worth surfacing.</summary>
    private static readonly TimeSpan ClientLookback = TimeSpan.FromDays(14);

    /// <summary>How far ahead a domain expiry is worth surfacing.</summary>
    private static readonly TimeSpan DomainLookahead = TimeSpan.FromDays(30);

    /// <summary>The default and the ceiling for how many events a caller may ask for.</summary>
    private const int DefaultLimit = 10;

    /// <summary>The most events this feed will ever return in one call.</summary>
    private const int MaxLimit = 50;

    /// <summary>The data context, for recently registered clients, overdue invoices and domains nearing expiry.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context, for recently registered clients, overdue invoices and domains nearing expiry.</param>
    /// <param name="clock">The source of the current moment.</param>
    public GetRecentActivityHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
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
        var limit = query.Limit is > 0 and <= MaxLimit ? query.Limit : DefaultLimit;
        var now = _clock.UtcNow;

        var lookbackStart = now - ClientLookback;
        var recentClients = await _context.Clients
            .Where(c => c.CreatedAt >= lookbackStart && c.CreatedAt <= now)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);
        var overdueInvoices = await _context.Invoices
            .AsNoTracking()
            .Where(x => x.Status == InvoiceStatus.Overdue)
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
        var expiryThreshold = now + DomainLookahead;
        var expiringDomains = await _context.Domains
            .IncludeChildren()
            .Where(x => x.ExpiresAt < expiryThreshold && x.Status == DomainStatus.Active)
            .ToListAsync(ct);

        var events = new List<ActivityEventDto>(recentClients.Count + overdueInvoices.Count + expiringDomains.Count);

        events.AddRange(recentClients.Select(c => new ActivityEventDto(
            ActivityEventType.ClientRegistered,
            $"{c.FirstName} {c.LastName} registered",
            c.CreatedAt,
            c.Id)));

        events.AddRange(overdueInvoices.Select(i => new ActivityEventDto(
            ActivityEventType.InvoiceOverdue,
            $"Invoice #{i.Id} overdue",
            i.DueDate,
            i.Id)));

        events.AddRange(expiringDomains.Select(d => new ActivityEventDto(
            ActivityEventType.DomainExpiring,
            $"{d.Name} expiring soon",
            d.ExpiresAt,
            d.Id)));

        IReadOnlyList<ActivityEventDto> merged = events
            .OrderByDescending(e => e.OccurredAt)
            .Take(limit)
            .ToList();

        return Result<IReadOnlyList<ActivityEventDto>>.Success(merged);
    }
}
