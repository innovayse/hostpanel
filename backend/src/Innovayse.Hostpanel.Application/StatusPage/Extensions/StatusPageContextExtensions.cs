using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.StatusPage.Extensions;

/// <summary>Reads over the status page tables that more than one use case needs, stated once.</summary>
public static class StatusPageContextExtensions
{
    /// <summary>Finds the components a maintenance window that is under way affects.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="now">The moment to judge the windows by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The ids of the components under maintenance now. A window counts from its start up to, not including, its end, and
    /// not once it was cancelled: the same rule as <c>StatusMaintenance.StateAt</c>, written as a query because EF Core
    /// cannot call it.
    /// </returns>
    public static async Task<IReadOnlyList<int>> StatusComponentIdsUnderMaintenanceAsync(
        this IHostpanelDbContext context, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        return await context.StatusMaintenances
            .AsNoTracking()
            .Where(m => m.CancelledAt == null && m.ScheduledStart <= now && m.ScheduledEnd > now)
            .SelectMany(m => m.Components.Select(c => c.ComponentId))
            .Distinct()
            .ToListAsync(ct);
    }

    /// <summary>Checks that every id names a status page component.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="componentIds">The ids to look for; repeated ids count once.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when every id names a component.</returns>
    public static async Task<bool> AllStatusComponentsExistAsync(
        this IHostpanelDbContext context, IReadOnlyCollection<int> componentIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(componentIds);

        var wanted = componentIds.Distinct().ToList();
        return await context.StatusComponents.CountAsync(c => wanted.Contains(c.Id), ct) == wanted.Count;
    }
}
