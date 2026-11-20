using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Commands.RemoveTerminatedServiceHostingResources;

/// <summary>
/// Marks every website and database plugins still held on a terminated service as removed. A terminated hosting account
/// takes its sites and databases with it, so no server is called (unlike an SSH key, which a termination that did not
/// reach the server could leave logging in): the records only stop saying something exists, which also frees the host
/// names and labels. Each resource is one line in the client's activity log, never naming a secret.
/// </summary>
public sealed class RemoveTerminatedServiceHostingResourcesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Stages the activity log line in the same save as the records.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="activity">Stages the activity log line in the same save as the records.</param>
    public RemoveTerminatedServiceHostingResourcesHandler(IHostpanelDbContext context, IActivityLogger activity)
    {
        _context = context;
        _activity = activity;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The terminated service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, also when there was nothing left to settle, or <c>SERVICE_NOT_FOUND</c> when the id names no service, or
    /// <c>SERVICE_NOT_TERMINATED</c> when the service is still running.
    /// </returns>
    public async Task<Result> HandleAsync(RemoveTerminatedServiceHostingResourcesCommand cmd, CancellationToken ct)
    {
        // Read-only here: only the status and the owner are checked; the resources are what change.
        var service = await _context.ClientServices
            .AsNoTracking()
            .Where(s => s.Id == cmd.ServiceId)
            .Select(s => new { s.ClientId, s.Status })
            .FirstOrDefaultAsync(ct);
        if (service is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        if (service.Status is not ServiceStatus.Terminated)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_TERMINATED);
        }

        var websites = await _context.ServiceWebsites
            .Where(w => w.ClientServiceId == cmd.ServiceId && w.State != HostingResourceState.Removed)
            .ToListAsync(ct);
        var databases = await _context.ServiceDatabases
            .Where(d => d.ClientServiceId == cmd.ServiceId && d.State != HostingResourceState.Removed)
            .ToListAsync(ct);

        foreach (var website in websites)
        {
            website.MarkRemoved();
            await StageLineAsync(service.ClientId, $"website {website.Hostname}", cmd.ServiceId, website, ct);
        }

        foreach (var database in databases)
        {
            database.MarkRemoved();
            await StageLineAsync(service.ClientId, $"database {database.Name}", cmd.ServiceId, database, ct);
        }

        if (websites.Count > 0 || databases.Count > 0)
        {
            await _context.SaveChangesAsync(ct);
        }

        return Result.Success();
    }

    /// <summary>Stages the activity line of one resource settled with the service.</summary>
    /// <param name="clientId">The service's client.</param>
    /// <param name="what">What was settled, for example <c>website example.com</c>; never a secret.</param>
    /// <param name="serviceId">The terminated service.</param>
    /// <param name="resource">The resource, whose plugin the line names.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the line is staged.</returns>
    private Task StageLineAsync(int clientId, string what, int serviceId, HostingResource resource, CancellationToken ct)
    {
        return _activity.RecordAsync(
            new ActivityLogEntry(
                clientId,
                $"Marked {what} issued to plugin {resource.IssuedToPluginId} as removed with terminated service #{serviceId}"),
            ct);
    }
}
