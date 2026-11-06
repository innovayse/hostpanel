using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListClaimedDomainRenewals;

/// <summary>Handles <see cref="ListClaimedDomainRenewalsQuery"/>.</summary>
public sealed class ListClaimedDomainRenewalsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, for the Clients module's client briefs.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The message bus, for the Clients module's client briefs.</param>
    public ListClaimedDomainRenewalsHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Lists the claimed lines.</summary>
    /// <param name="query">The query; it carries no filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Every claimed, unapplied domain renewal line, oldest claim first. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<ClaimedDomainRenewalDto>>> HandleAsync(ListClaimedDomainRenewalsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var lines = await _context.Invoices.AsNoTracking()
            .SelectMany(i => i.Items.Select(l => new { Invoice = i, Line = l }))
            .Where(x => x.Line.RegisteredDomainId != null && x.Line.RenewalPeriodStart != null
                        && x.Line.RenewalAppliedAt == null && x.Line.RenewalAttemptAt != null)
            .OrderBy(x => x.Line.RenewalAttemptAt).ThenBy(x => x.Line.Id)
            .Select(x => new
            {
                InvoiceId = x.Invoice.Id,
                ItemId = x.Line.Id,
                x.Invoice.ClientId,
                DomainId = x.Line.RegisteredDomainId!.Value,
                x.Line.Description,
                Start = x.Line.RenewalPeriodStart!.Value,
                End = x.Line.RenewalPeriodEnd!.Value,
                ClaimedAt = x.Line.RenewalAttemptAt!.Value,
            })
            .ToListAsync(ct);

        // The client's name belongs to the Clients module, so it is asked for through its brief, once for all the lines.
        var clients = await _bus.GetRequiredClientBriefsByIdAsync(lines.Select(l => l.ClientId).Distinct().ToList(), ct);
        var claims = lines
            .Select(l => new ClaimedDomainRenewalDto(
                l.InvoiceId, l.ItemId, l.ClientId, clients[l.ClientId].FullName, l.DomainId, l.Description, l.Start, l.End, l.ClaimedAt))
            .ToList();

        return Result<IReadOnlyList<ClaimedDomainRenewalDto>>.Success(claims);
    }
}
