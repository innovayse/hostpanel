using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.StatusPage.Common;
using Innovayse.Hostpanel.Application.StatusPage.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.StatusPage.Queries.ListStatusMaintenance;

/// <summary>Loads the maintenance windows that are under way or still to come: the ones that ended or were cancelled are left out.</summary>
public sealed class ListStatusMaintenanceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the windows are judged by.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the windows are judged by.</param>
    public ListStatusMaintenanceHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The query; it carries no input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The windows, soonest start first. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<StatusMaintenanceDto>>> HandleAsync(ListStatusMaintenanceQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var now = _clock.UtcNow;
        var windows = await _context.StatusMaintenances
            .AsNoTracking()
            .Where(m => m.CancelledAt == null && m.ScheduledEnd > now)
            .OrderBy(m => m.ScheduledStart)
            .ThenBy(m => m.Id)
            .ToListAsync(ct);

        return Result<IReadOnlyList<StatusMaintenanceDto>>.Success(windows.Select(m => m.ToDto(now)).ToList());
    }
}
