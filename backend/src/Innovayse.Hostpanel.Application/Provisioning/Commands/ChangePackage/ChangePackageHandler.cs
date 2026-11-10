using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Provisioning.Commands.ChangePackage;

/// <summary>
/// Changes the hosting package of a provisioned account by calling the provisioning provider.
/// </summary>
public sealed class ChangePackageHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the service's server from the Admin module.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the provisioning provider configured for the service's server.</summary>
    private readonly IProvisioningProviderFactory _providerFactory;

    /// <summary>Logs a provider failure without putting its detail on the wire.</summary>
    private readonly ILogger<ChangePackageHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Reads the service's server from the Admin module.</param>
    /// <param name="providerFactory">Creates the provisioning provider configured for the service's server.</param>
    /// <param name="logger">Logs a provider failure without putting its detail on the wire.</param>
    public ChangePackageHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        IProvisioningProviderFactory providerFactory,
        ILogger<ChangePackageHandler> logger)
    {
        _context = context;
        _bus = bus;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    /// <param name="cmd">The service to reassign and the hosting package to give it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>SERVICE_NOT_FOUND</c> when the id names no service,
    /// <c>SERVICE_NOT_PROVISIONED</c> when the service has never been provisioned,
    /// <c>SERVICE_NOT_ACTIVE</c> when the service is terminated and its account removed,
    /// <c>SERVICE_NO_SERVER_ASSIGNED</c> when it has no usable server, or
    /// <c>SERVICE_PACKAGE_UPSTREAM_FAILED</c> when the provider rejects the change.
    /// </returns>
    public async Task<Result> HandleAsync(ChangePackageCommand cmd, CancellationToken ct)
    {
        var service = await _context.ClientServices.FirstOrDefaultAsync(s => s.Id == cmd.ServiceId, ct);
        if (service is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        var account = service.RequireHostingAccount();
        if (account.IsFailure)
        {
            return Result.FailureCode(account.ErrorCode!);
        }

        var server = await _bus.FindServerAsync(service, ct);
        if (server is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NO_SERVER_ASSIGNED);
        }

        try
        {
            var provider = _providerFactory.CreateFor(server);
            await provider.ChangePackageAsync(account.Value!, cmd.NewPackage, ct);
        }
        catch (Exception ex)
        {
            // The provider's own message and the server it named never reach the caller (rules
            // on upstream detail); they are logged here for whoever has to chase the failure up.
            _logger.LogWarning(
                ex, "Changing package for service {ServiceId} on server {ServerId} failed.", service.Id, server.Id);
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_PACKAGE_UPSTREAM_FAILED);
        }

        return Result.Success();
    }
}
