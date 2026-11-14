using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Queries.GetServerSshTarget;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.RemoveTerminatedAccountJail;

/// <summary>Handles <see cref="RemoveTerminatedAccountJailCommand"/> through the server's SSH key agent (op <c>remove-jail</c>).</summary>
public sealed class RemoveTerminatedAccountJailHandler
{
    /// <summary>Reads the service.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Asks the Servers module for the server's agent settings.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the server's key agent client.</summary>
    private readonly ISshKeyProviderFactory _factory;

    /// <summary>Records the outcome for staff.</summary>
    private readonly ILogger<RemoveTerminatedAccountJailHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">Reads the service.</param>
    /// <param name="bus">Asks the Servers module for the server's agent settings.</param>
    /// <param name="factory">Creates the server's key agent client.</param>
    /// <param name="logger">Records the outcome for staff.</param>
    public RemoveTerminatedAccountJailHandler(
        IHostpanelDbContext context, IMessageBus bus, ISshKeyProviderFactory factory, ILogger<RemoveTerminatedAccountJailHandler> logger)
    {
        _context = context;
        _bus = bus;
        _factory = factory;
        _logger = logger;
    }

    /// <summary>Asks the agent to remove the jail; a server without a usable agent, or an older agent, is left alone.</summary>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success unless the service does not exist; refusals are logged, never retried.</returns>
    public async Task<Result> HandleAsync(RemoveTerminatedAccountJailCommand cmd, CancellationToken ct)
    {
        var service = await _context.ClientServices.AsNoTracking()
            .Where(s => s.Id == cmd.ServiceId)
            .Select(s => new { s.ServerId, s.ProvisioningRef })
            .FirstOrDefaultAsync(ct);
        if (service is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        // The hosting account name is the provisioning reference every provider call already uses.
        if (service.ServerId is not int serverId || string.IsNullOrWhiteSpace(service.ProvisioningRef))
        {
            return Result.Success();
        }

        var lookup = await _bus.InvokeAsync<Result<ServerSshTargetDto>>(new GetServerSshTargetQuery(serverId), ct);
        if (lookup.IsFailure || lookup.Value!.IsDisabled || lookup.Value.Agent is not { IsEnabled: true, IsComplete: true }
            || !_factory.Supports(lookup.Value.Module) || _factory.CreateFor(lookup.Value) is not { } provider)
        {
            return Result.Success();
        }

        try
        {
            var removed = await provider.RemoveDeletedAccountJailAsync(new SshAccount(serverId, service.ProvisioningRef), ct);
            if (removed)
            {
                _logger.LogInformation("Removed the SSH jail of terminated service {ServiceId} on server {ServerId}.", cmd.ServiceId, serverId);
            }
        }
        catch (SshProviderException ex) when (ex.Failure is SshProviderFailure.UnknownOperation or SshProviderFailure.InvalidRequest)
        {
            _logger.LogInformation("The key agent of server {ServerId} cannot remove jails yet (agent older than 1.4.0).", serverId);
        }
        catch (SshProviderException ex)
        {
            _logger.LogWarning(
                "Removing the SSH jail of terminated service {ServiceId} on server {ServerId} was refused: {Failure}",
                cmd.ServiceId, serverId, ex.Failure);
        }

        return Result.Success();
    }
}
