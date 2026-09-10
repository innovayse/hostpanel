namespace Innovayse.Application.Admin.Queries.GetRecentActivity;

using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Handles <see cref="GetRecentActivityQuery"/> by pulling the newest client registration,
/// invoice-overdue and domain-expiry events from their own repositories and merging them by time.
/// </summary>
/// <param name="clientRepo">Client repository, for recently registered clients.</param>
/// <param name="invoiceRepo">Invoice repository, for overdue invoices.</param>
/// <param name="domainRepo">Domain repository, for domains nearing expiry.</param>
public sealed class GetRecentActivityHandler(
    IClientRepository clientRepo,
    IInvoiceRepository invoiceRepo,
    IDomainRepository domainRepo)
{
    /// <summary>How far back a client registration is still worth surfacing.</summary>
    private static readonly TimeSpan ClientLookback = TimeSpan.FromDays(14);

    /// <summary>How far ahead a domain expiry is worth surfacing.</summary>
    private static readonly TimeSpan DomainLookahead = TimeSpan.FromDays(30);

    /// <summary>
    /// Builds the merged, newest-first activity feed.
    /// </summary>
    /// <param name="query">The query request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Up to <see cref="GetRecentActivityQuery.Limit"/> events, newest first.</returns>
    public async Task<IReadOnlyList<ActivityEventDto>> HandleAsync(GetRecentActivityQuery query, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var recentClients = await clientRepo.GetCreatedBetweenAsync(now - ClientLookback, now, ct);
        var (overdueInvoices, _) = await invoiceRepo.ListAsync(1, query.Limit, InvoiceStatus.Overdue, null, null, ct);
        var expiringDomains = await domainRepo.ListExpiringBeforeAsync(now + DomainLookahead, ct);

        var events = new List<ActivityEventDto>(recentClients.Count + overdueInvoices.Count + expiringDomains.Count);

        events.AddRange(recentClients.Select(c => new ActivityEventDto(
            ActivityEventType.ClientRegistered,
            $"{c.FirstName} {c.LastName} registered",
            c.CreatedAt)));

        events.AddRange(overdueInvoices.Select(i => new ActivityEventDto(
            ActivityEventType.InvoiceOverdue,
            $"Invoice #{i.Id} overdue",
            i.DueDate)));

        events.AddRange(expiringDomains.Select(d => new ActivityEventDto(
            ActivityEventType.DomainExpiring,
            $"{d.Name} expiring soon",
            d.ExpiresAt)));

        return events
            .OrderByDescending(e => e.OccurredAt)
            .Take(query.Limit)
            .ToList();
    }
}
