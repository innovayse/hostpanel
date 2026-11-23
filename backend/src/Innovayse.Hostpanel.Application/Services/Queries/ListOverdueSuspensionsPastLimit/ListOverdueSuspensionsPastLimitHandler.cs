using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Extensions;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Queries.ListOverdueSuspensionsPastLimit;

/// <summary>Handles <see cref="ListOverdueSuspensionsPastLimitQuery"/> with one read.</summary>
public sealed class ListOverdueSuspensionsPastLimitHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListOverdueSuspensionsPastLimitHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Lists the services.</summary>
    /// <param name="query">The latest suspension moment that is past the limit.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ids, ordered. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<int>>> HandleAsync(ListOverdueSuspensionsPastLimitQuery query, CancellationToken ct)
    {
        // Only a non-payment suspension runs out: one staff, a domain expiry or an import put in
        // place stays until someone lifts it. A service billed by hand (AutoRenew off) is left to
        // staff as well, even when it was suspended for non-payment before it was switched over.
        // A terminated service is no longer suspended, so a rerun of the caller finds nothing left to do.
        var ids = await _context.ClientServices.AsNoTracking().NotInTrial()
            .Where(s => s.Status == ServiceStatus.Suspended
                && s.SuspensionSource == SuspensionSource.Overdue
                && s.AutoRenew
                && s.SuspendedAt != null
                && s.SuspendedAt <= query.SuspendedBy)
            .OrderBy(s => s.Id)
            .Select(s => s.Id)
            .ToListAsync(ct);

        return Result<IReadOnlyList<int>>.Success(ids);
    }
}
