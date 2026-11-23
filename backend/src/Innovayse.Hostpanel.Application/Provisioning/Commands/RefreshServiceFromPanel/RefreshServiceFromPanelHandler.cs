using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Application.Provisioning.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Provisioning.Commands.RefreshServiceFromPanel;

/// <summary>
/// Reads a service's hosting account from its panel and records the IP address the panel reports, when it is a valid one. The panel is
/// only read, never written to, so it is safe to run after every provisioning and as often as staff like.
/// </summary>
public sealed class RefreshServiceFromPanelHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the service's server from the Admin module.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the provisioning provider configured for the service's server.</summary>
    private readonly IProvisioningProviderFactory _providerFactory;

    /// <summary>Logs a provider failure without putting its detail on the wire.</summary>
    private readonly ILogger<RefreshServiceFromPanelHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Reads the service's server from the Admin module.</param>
    /// <param name="providerFactory">Creates the provisioning provider configured for the service's server.</param>
    /// <param name="logger">Logs a provider failure without putting its detail on the wire.</param>
    public RefreshServiceFromPanelHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        IProvisioningProviderFactory providerFactory,
        ILogger<RefreshServiceFromPanelHandler> logger)
    {
        _context = context;
        _bus = bus;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The service to refresh.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>SERVICE_NOT_FOUND</c> when the id names no service, <c>SERVICE_NOT_PROVISIONED</c> when the service has never
    /// been provisioned, <c>SERVICE_NOT_ACTIVE</c> when it is terminated, <c>SERVICE_NO_SERVER_ASSIGNED</c> when it has no usable server,
    /// the panel's own refusal code (<see cref="PanelErrorCodes"/>) when a panel plugin refuses with a reason, or
    /// <c>SERVICE_PANEL_SYNC_UPSTREAM_FAILED</c> when the panel cannot be read.
    /// </returns>
    public async Task<Result> HandleAsync(RefreshServiceFromPanelCommand cmd, CancellationToken ct)
    {
        var service = await _context.ClientServices.FirstOrDefaultAsync(s => s.Id == cmd.ServiceId, ct);
        if (service is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        var read = await _bus.ReadHostedAccountAsync(_providerFactory, service, _logger, ct);
        if (read.IsFailure)
        {
            return Result.FailureCode(read.ErrorCode!);
        }

        // An address the panel does not report, or reports in a shape that is no address, leaves what the service holds alone.
        var address = PanelIpAddress.Normalize(read.Value!.IpAddress);
        if (address is not null && !string.Equals(address, service.DedicatedIp, StringComparison.Ordinal))
        {
            service.RecordAssignedIp(address);
            await _context.SaveChangesAsync(ct);
        }

        return Result.Success();
    }
}
