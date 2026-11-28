using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Application.Provisioning.Extensions;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Provisioning.Queries.GetPanelSsoUrl;

/// <summary>
/// Generates a time-limited control-panel sign-in link for direct client access to their hosting account.
/// </summary>
public sealed class GetPanelSsoUrlHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the service's server from the Admin module.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the provisioning provider configured for the service's server.</summary>
    private readonly IProvisioningProviderFactory _providerFactory;

    /// <summary>The caller, whose address a panel may bind the sign-in session to.</summary>
    private readonly ICurrentUserService _caller;

    /// <summary>Logs a provider failure without putting its detail on the wire.</summary>
    private readonly ILogger<GetPanelSsoUrlHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Reads the service's server from the Admin module.</param>
    /// <param name="providerFactory">Creates the provisioning provider configured for the service's server.</param>
    /// <param name="caller">The caller, whose address a panel may bind the sign-in session to.</param>
    /// <param name="logger">Logs a provider failure without putting its detail on the wire.</param>
    public GetPanelSsoUrlHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        IProvisioningProviderFactory providerFactory,
        ICurrentUserService caller,
        ILogger<GetPanelSsoUrlHandler> logger)
    {
        _context = context;
        _bus = bus;
        _providerFactory = providerFactory;
        _caller = caller;
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
    /// <c>SERVICE_HOSTING_ACCOUNT_REMOVED</c> when the service is terminated and its account removed,
    /// <c>SERVICE_NO_SERVER_ASSIGNED</c> when it has no usable server,
    /// <c>SERVICE_PANEL_SSO_URL_UPSTREAM_FAILED</c> when the panel answered an address that is not an
    /// https address on its own server, or the panel's own refusal code (<see cref="PanelErrorCodes"/>) when a panel plugin refuses with a reason, or
    /// <c>SERVICE_PANEL_SSO_UPSTREAM_FAILED</c> when the provider cannot issue one.
    /// </returns>
    public async Task<Result<PanelSsoDto>> HandleAsync(GetPanelSsoUrlQuery query, CancellationToken ct)
    {
        var service = await _context.ClientServices.FirstOrDefaultAsync(s => s.Id == query.ServiceId, ct);
        if (service is null)
        {
            return Result<PanelSsoDto>.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        // A terminated service's reference outlives its account: the panel no longer holds it, so
        // asking would only come back as an upstream failure that reads like an outage.
        var account = service.RequireHostingAccount();
        if (account.IsFailure)
        {
            return Result<PanelSsoDto>.FailureCode(account.ErrorCode!);
        }

        var server = await _bus.FindServerAsync(service, ct);
        if (server is null)
        {
            return Result<PanelSsoDto>.FailureCode(HostpanelErrorCodes.SERVICE_NO_SERVER_ASSIGNED);
        }

        // The browser that asked is the one that opens the link. Some panels open a
        // session only from the address it was issued for; behind the portal's BFF this is the
        // visitor's address, forwarded in X-Forwarded-For and resolved by the trusted-proxy step.
        // SERVICE_PANEL_SSO_URL_UPSTREAM_FAILED is the provider saying the panel named an address that is not on its own
        // server: the caller only learns that the session was not accepted.
        var url = await PanelCall.RunAsync(
            () => _providerFactory.CreateFor(server).GetPanelSsoUrlAsync(new PanelSsoOrder(service.Id, account.Value!, _caller.IpAddress), ct),
            _logger, "Issuing a control panel SSO URL", service.Id, server.Id, HostpanelErrorCodes.SERVICE_PANEL_SSO_UPSTREAM_FAILED,
            HostpanelErrorCodes.SERVICE_PANEL_SSO_URL_UPSTREAM_FAILED);
        return url.IsSuccess
            ? Result<PanelSsoDto>.Success(new PanelSsoDto(url.Value!))
            : Result<PanelSsoDto>.FailureCode(url.ErrorCode!);
    }
}
