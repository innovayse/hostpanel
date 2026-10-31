using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Application.Provisioning.Extensions;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Provisioning.Queries.GetControlPanelSsoUrl;

/// <summary>
/// Generates a time-limited control-panel sign-in link for direct client access to their hosting account.
/// </summary>
public sealed class GetControlPanelSsoUrlHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the service's server from the Admin module.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the provisioning provider configured for the service's server.</summary>
    private readonly IProvisioningProviderFactory _providerFactory;

    /// <summary>Logs a provider failure without putting its detail on the wire.</summary>
    private readonly ILogger<GetControlPanelSsoUrlHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Reads the service's server from the Admin module.</param>
    /// <param name="providerFactory">Creates the provisioning provider configured for the service's server.</param>
    /// <param name="logger">Logs a provider failure without putting its detail on the wire.</param>
    public GetControlPanelSsoUrlHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        IProvisioningProviderFactory providerFactory,
        ILogger<GetControlPanelSsoUrlHandler> logger)
    {
        _context = context;
        _bus = bus;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Handles the query.
    /// </summary>
    /// <param name="query">The service to sign into.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A time-limited control-panel sign-in link, or <c>SERVICE_NOT_FOUND</c> when the id names no service,
    /// <c>SERVICE_NOT_PROVISIONED</c> when the service has never been provisioned,
    /// <c>SERVICE_NOT_ACTIVE</c> when the service is terminated and its account removed,
    /// <c>SERVICE_NO_SERVER_ASSIGNED</c> when it has no usable server, or
    /// <c>SERVICE_CPANEL_SSO_UPSTREAM_FAILED</c> when the provider cannot issue one.
    /// </returns>
    public async Task<Result<ControlPanelSsoDto>> HandleAsync(GetControlPanelSsoUrlQuery query, CancellationToken ct)
    {
        var service = await _context.ClientServices.FirstOrDefaultAsync(s => s.Id == query.ServiceId, ct);
        if (service is null)
        {
            return Result<ControlPanelSsoDto>.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        if (service.ProvisioningRef is null)
        {
            return Result<ControlPanelSsoDto>.FailureCode(HostpanelErrorCodes.SERVICE_NOT_PROVISIONED);
        }

        // A terminated service's reference outlives its account: the panel no longer holds it, so
        // asking would only come back as an upstream failure that reads like an outage.
        if (service.Status == ServiceStatus.Terminated)
        {
            return Result<ControlPanelSsoDto>.FailureCode(HostpanelErrorCodes.SERVICE_NOT_ACTIVE);
        }

        var server = await _bus.FindServerAsync(service, ct);
        if (server is null)
        {
            return Result<ControlPanelSsoDto>.FailureCode(HostpanelErrorCodes.SERVICE_NO_SERVER_ASSIGNED);
        }

        try
        {
            var provider = _providerFactory.CreateFor(server);
            var url = await provider.GetCPanelSsoUrlAsync(service.ProvisioningRef, ct);
            return Result<ControlPanelSsoDto>.Success(new ControlPanelSsoDto(url));
        }
        catch (Exception ex)
        {
            // The provider's own message and the server it named never reach the caller (rules
            // on upstream detail); they are logged here for whoever has to chase the failure up.
            _logger.LogWarning(
                ex, "Issuing a control panel SSO URL for service {ServiceId} on server {ServerId} failed.",
                service.Id, server.Id);
            return Result<ControlPanelSsoDto>.FailureCode(HostpanelErrorCodes.SERVICE_CPANEL_SSO_UPSTREAM_FAILED);
        }
    }
}
