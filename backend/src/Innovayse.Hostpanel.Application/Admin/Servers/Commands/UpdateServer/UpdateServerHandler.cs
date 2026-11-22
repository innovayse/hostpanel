using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Commands.UpdateServer;

/// <summary>
/// Handles <see cref="UpdateServerCommand"/> by finding and updating the target server.
/// </summary>
public sealed class UpdateServerHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The control panel modules a server can be given.</summary>
    private readonly IServerModuleCatalog _modules;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="modules">The control panel modules a server can be given.</param>
    public UpdateServerHandler(IHostpanelDbContext context, IServerModuleCatalog modules)
    {
        _context = context;
        _modules = modules;
    }

    /// <summary>
    /// Updates the server.
    /// </summary>
    /// <param name="cmd">The updated configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>SERVER_NOT_FOUND</c> when the id names no server, or <c>SERVER_MODULE_UNKNOWN</c> when neither the host, an installed plugin nor the server's own stored module knows it.</returns>
    public async Task<Result> HandleAsync(UpdateServerCommand cmd, CancellationToken ct)
    {
        var server = await _context.Servers.FirstOrDefaultAsync(s => s.Id == cmd.Id, ct);
        if (server is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVER_NOT_FOUND);
        }

        var module = _modules.Canonical(cmd.Module, server.Module);
        if (module is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVER_MODULE_UNKNOWN);
        }

        var details = new ServerDetails(
            cmd.Name, cmd.Endpoint.Hostname, cmd.Endpoint.IpAddress, cmd.Endpoint.AssignedIpAddresses,
            module, cmd.Username, cmd.Credentials, cmd.Endpoint.UseSSL,
            cmd.Limits.MaxAccounts, cmd.Limits.IsDefault, cmd.Limits.IsDisabled,
            cmd.Hosting.MonthlyCost, cmd.Hosting.Datacenter, cmd.Hosting.ServerStatusAddress,
            cmd.NameServers, cmd.Endpoint.AcceptsSelfSignedCertificate, cmd.Endpoint.ApiHost);

        server.Reconfigure(details, cmd.Clears ?? ServerCredentialClears.None);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
