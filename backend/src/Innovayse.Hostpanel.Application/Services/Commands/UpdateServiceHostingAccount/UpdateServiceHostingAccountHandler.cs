using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Queries.GetServerConnection;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.UpdateServiceHostingAccount;

/// <summary>Handles <see cref="UpdateServiceHostingAccountCommand"/>: records the hosting account details on the service.</summary>
public sealed class UpdateServiceHostingAccountHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, used to ask the servers module whether the chosen server exists.</summary>
    private readonly IMessageBus _bus;

    /// <summary>What the server's control panel accepts as an account username.</summary>
    private readonly IPanelAccountRules _panelRules;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The message bus.</param>
    /// <param name="panelRules">What the server's control panel accepts as an account username.</param>
    public UpdateServiceHostingAccountHandler(IHostpanelDbContext context, IMessageBus bus, IPanelAccountRules panelRules)
    {
        _context = context;
        _bus = bus;
        _panelRules = panelRules;
    }

    /// <summary>Records the hosting account details on the service.</summary>
    /// <param name="cmd">The hosting account details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, <c>SERVICE_NOT_FOUND</c> when the id names no service, <c>SERVER_NOT_FOUND</c> when the
    /// chosen server does not exist, or <c>CPANEL_USERNAME_INVALID</c> when the server's panel would
    /// not accept the username (a new account's rule while the service is Pending, the looser rule
    /// for an account that already exists otherwise).
    /// </returns>
    public async Task<Result> HandleAsync(UpdateServiceHostingAccountCommand cmd, CancellationToken ct)
    {
        var service = await _context.ClientServices.FirstOrDefaultAsync(s => s.Id == cmd.ServiceId, ct);
        if (service is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        if (cmd.ServerId is int serverId)
        {
            var server = await _bus.InvokeAsync<Result<ServerConnectionDto>>(new GetServerConnectionQuery(serverId), ct);
            if (!server.IsSuccess)
            {
                return Result.FailureCode(HostpanelErrorCodes.SERVER_NOT_FOUND);
            }

            // Checked here and not in the validator: the rule belongs to the server's panel, which
            // only the server record knows. A name the panel would refuse fails now, not at provisioning.
            if (!string.IsNullOrEmpty(cmd.Username) && !AcceptsUsername(server.Value!.Module, cmd.Username, service.Status))
            {
                return Result.FailureCode(HostpanelErrorCodes.CPANEL_USERNAME_INVALID);
            }
        }

        service.RecordHostingAccount(new ServiceHostingAccount(
            cmd.ServerId,
            cmd.Domain,
            cmd.ProvisioningRef,
            cmd.DedicatedIp,
            cmd.Username,
            cmd.Password));

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Whether the panel accepts the username for a service in the given status.</summary>
    /// <param name="module">The server's control panel module.</param>
    /// <param name="username">The username staff entered.</param>
    /// <param name="status">The service's status: Pending means the account is still to be created.</param>
    /// <returns><see langword="true"/> when the panel accepts the name.</returns>
    private bool AcceptsUsername(ServerModule module, string username, ServiceStatus status)
    {
        return status is ServiceStatus.Pending
            ? _panelRules.AcceptsNewUsername(module, username)
            : _panelRules.AcceptsAccountUsername(module, username);
    }
}
