using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListClaimedDomainRenewals;

/// <summary>Handles <see cref="ListClaimedDomainRenewalsQuery"/>.</summary>
public sealed class ListClaimedDomainRenewalsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListClaimedDomainRenewalsHandler(IHostpanelDbContext context)
    {
        _context = context;
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
            .Select(x => new ClaimedDomainRenewalDto(
                x.Invoice.Id, x.Line.Id, x.Invoice.ClientId, x.Line.RegisteredDomainId!.Value, x.Line.Description,
                x.Line.RenewalPeriodStart!.Value, x.Line.RenewalPeriodEnd!.Value, x.Line.RenewalAttemptAt!.Value))
            .ToListAsync(ct);

        return Result<IReadOnlyList<ClaimedDomainRenewalDto>>.Success(lines);
    }
}
