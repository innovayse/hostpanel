using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Application.Provisioning.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Provisioning.Queries.GetServiceCredentials;

/// <summary>
/// Retrieves the hosting credentials for a provisioned service from the provisioning provider
/// configured for the service's own server.
/// </summary>
public sealed class GetServiceCredentialsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the service's server from the Admin module.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the provisioning provider configured for the service's server.</summary>
    private readonly IProvisioningProviderFactory _providerFactory;

    /// <summary>Logs a provider failure without putting its detail on the wire.</summary>
    private readonly ILogger<GetServiceCredentialsHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Reads the service's server from the Admin module.</param>
    /// <param name="providerFactory">Creates the provisioning provider configured for the service's server.</param>
    /// <param name="logger">Logs a provider failure without putting its detail on the wire.</param>
    public GetServiceCredentialsHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        IProvisioningProviderFactory providerFactory,
        ILogger<GetServiceCredentialsHandler> logger)
    {
        _context = context;
        _bus = bus;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Handles the query.
    /// </summary>
    /// <param name="query">The service to read credentials for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The service's current credentials, or <c>SERVICE_NOT_FOUND</c> when the id names no
    /// service, <c>SERVICE_NOT_PROVISIONED</c> when the service has never been provisioned,
    /// <c>SERVICE_NOT_ACTIVE</c> when the service is terminated and its account removed,
    /// <c>SERVICE_NO_SERVER_ASSIGNED</c> when it has no usable server, or
    /// <c>SERVICE_CREDENTIALS_UPSTREAM_FAILED</c> when the provider cannot return them.
    /// </returns>
    public async Task<Result<ServiceCredentialsDto>> HandleAsync(GetServiceCredentialsQuery query, CancellationToken ct)
    {
        var service = await _context.ClientServices.FirstOrDefaultAsync(s => s.Id == query.ServiceId, ct);
        if (service is null)
        {
            return Result<ServiceCredentialsDto>.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        var account = service.RequireHostingAccount();
        if (account.IsFailure)
        {
            return Result<ServiceCredentialsDto>.FailureCode(account.ErrorCode!);
        }

        var server = await _bus.FindServerAsync(service, ct);
        if (server is null)
        {
            return Result<ServiceCredentialsDto>.FailureCode(HostpanelErrorCodes.SERVICE_NO_SERVER_ASSIGNED);
        }

        try
        {
            var provider = _providerFactory.CreateFor(server);
            var credentials = await provider.GetCredentialsAsync(account.Value!, ct);
            return Result<ServiceCredentialsDto>.Success(new ServiceCredentialsDto(
                credentials.Username,
                credentials.Password,
                credentials.Domain,
                credentials.ServerIp,
                credentials.CpanelUrl));
        }
        catch (Exception ex)
        {
            // The provider's own message never reaches the caller (rules on upstream detail); it
            // is logged here for whoever has to chase the failure up.
            _logger.LogWarning(
                ex, "Reading credentials for service {ServiceId} on server {ServerId} failed.", service.Id, server.Id);
            return Result<ServiceCredentialsDto>.FailureCode(HostpanelErrorCodes.SERVICE_CREDENTIALS_UPSTREAM_FAILED);
        }
    }
}
