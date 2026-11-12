using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.ConfigureStatusComponentMonitoring;

/// <summary>Sets the address, the switch and the failure threshold of a component's automatic check, and persists them.</summary>
public sealed class ConfigureStatusComponentMonitoringHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ConfigureStatusComponentMonitoringHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The component and its monitoring settings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>STATUS_COMPONENT_NOT_FOUND</c> when the id names no component.</returns>
    public async Task<Result> HandleAsync(ConfigureStatusComponentMonitoringCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var component = await _context.StatusComponents.FirstOrDefaultAsync(c => c.Id == cmd.Id, ct);
        if (component is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.STATUS_COMPONENT_NOT_FOUND);
        }

        component.ConfigureMonitoring(cmd.CheckUrl, cmd.Enabled, cmd.FailureThreshold);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
