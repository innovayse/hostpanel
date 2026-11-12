using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.StatusPage.Common;
using Innovayse.Hostpanel.Application.StatusPage.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.StatusPage.Queries.ListAdminStatusMaintenance;

/// <summary>Loads every maintenance window for the admin panel: past, under way, upcoming and cancelled.</summary>
public sealed class ListAdminStatusMaintenanceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the windows are judged by.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the windows are judged by.</param>
    public ListAdminStatusMaintenanceHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The query; it carries no input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The windows, newest scheduled start first. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<StatusMaintenanceDto>>> HandleAsync(ListAdminStatusMaintenanceQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var now = _clock.UtcNow;
        var windows = await _context.StatusMaintenances
            .AsNoTracking()
            .OrderByDescending(m => m.ScheduledStart)
            .ThenByDescending(m => m.Id)
            .ToListAsync(ct);

        return Result<IReadOnlyList<StatusMaintenanceDto>>.Success(windows.Select(m => m.ToDto(now)).ToList());
    }
}
