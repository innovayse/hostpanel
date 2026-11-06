using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Commands.SetServiceAutoRenew;

/// <summary>
/// Handles <see cref="SetServiceAutoRenewCommand"/>: flips <c>AutoRenew</c> through the aggregate and records
/// the change in the client's activity log, saved together.
/// </summary>
public sealed class SetServiceAutoRenewHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Records the activity log line, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="activity">Records the activity log line, saved with the change it describes.</param>
    public SetServiceAutoRenewHandler(IHostpanelDbContext context, IActivityLogger activity)
    {
        _context = context;
        _activity = activity;
    }

    /// <summary>Sets the flag.</summary>
    /// <param name="cmd">The service and the value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>SERVICE_NOT_FOUND</c> when the id names no service.</returns>
    public async Task<Result> HandleAsync(SetServiceAutoRenewCommand cmd, CancellationToken ct)
    {
        var service = await _context.ClientServices.FirstOrDefaultAsync(s => s.Id == cmd.ServiceId, ct);
        if (service is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        service.SetAutoRenew(cmd.Value);
        await _activity.RecordAsync(
            new ActivityLogEntry(service.ClientId, $"Set Service Auto Renew - Service ID: {service.Id}, Auto Renew: {cmd.Value}"), ct);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
