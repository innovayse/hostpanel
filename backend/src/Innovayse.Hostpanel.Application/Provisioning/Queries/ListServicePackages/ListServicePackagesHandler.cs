using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Application.Provisioning.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Provisioning.Queries.ListServicePackages;

/// <summary>
/// Lists the hosting packages the server of a provisioned service offers, by asking the provisioning provider configured for that
/// server. A provider that cannot list answers <c>PANEL_OPERATION_NOT_SUPPORTED</c>, and the screen then asks for the name by hand.
/// </summary>
public sealed class ListServicePackagesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the service's server from the Admin module.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the provisioning provider configured for the service's server.</summary>
    private readonly IProvisioningProviderFactory _providerFactory;

    /// <summary>Logs a provider failure without putting its detail on the wire.</summary>
    private readonly ILogger<ListServicePackagesHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Reads the service's server from the Admin module.</param>
    /// <param name="providerFactory">Creates the provisioning provider configured for the service's server.</param>
    /// <param name="logger">Logs a provider failure without putting its detail on the wire.</param>
    public ListServicePackagesHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        IProvisioningProviderFactory providerFactory,
        ILogger<ListServicePackagesHandler> logger)
    {
        _context = context;
        _bus = bus;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The service whose server's packages are listed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The package names, or <c>SERVICE_NOT_FOUND</c> when the id names no service, <c>SERVICE_NOT_PROVISIONED</c> when the service has
    /// never been provisioned, <c>SERVICE_NOT_ACTIVE</c> when it is terminated, <c>SERVICE_NO_SERVER_ASSIGNED</c> when it has no usable
    /// server, the panel's own refusal code (<see cref="PanelErrorCodes"/>) when a panel plugin refuses, including
    /// <c>PANEL_OPERATION_NOT_SUPPORTED</c> when the provider cannot list its packages, or <c>SERVICE_PACKAGES_UPSTREAM_FAILED</c> when
    /// the provider fails.
    /// </returns>
    public async Task<Result<ServicePackagesDto>> HandleAsync(ListServicePackagesQuery query, CancellationToken ct)
    {
        var service = await _context.ClientServices.FirstOrDefaultAsync(s => s.Id == query.ServiceId, ct);
        if (service is null)
        {
            return Result<ServicePackagesDto>.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        var account = service.RequireHostingAccount();
        if (account.IsFailure)
        {
            return Result<ServicePackagesDto>.FailureCode(account.ErrorCode!);
        }

        var server = await _bus.FindServerAsync(service, ct);
        if (server is null)
        {
            return Result<ServicePackagesDto>.FailureCode(HostpanelErrorCodes.SERVICE_NO_SERVER_ASSIGNED);
        }

        var names = await PanelCall.RunAsync(
            () => _providerFactory.CreateFor(server).ListPackagesAsync(ct),
            _logger, "Listing packages", service.Id, server.Id, HostpanelErrorCodes.SERVICE_PACKAGES_UPSTREAM_FAILED);
        return names.IsFailure
            ? Result<ServicePackagesDto>.FailureCode(names.ErrorCode!)
            : Result<ServicePackagesDto>.Success(new ServicePackagesDto(names.Value!));
    }
}
