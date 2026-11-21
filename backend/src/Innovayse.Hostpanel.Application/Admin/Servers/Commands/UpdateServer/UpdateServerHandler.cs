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

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public UpdateServerHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Updates the server.
    /// </summary>
    /// <param name="cmd">The updated configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>SERVER_NOT_FOUND</c> when the id names no server.</returns>
    public async Task<Result> HandleAsync(UpdateServerCommand cmd, CancellationToken ct)
    {
        var server = await _context.Servers.FirstOrDefaultAsync(s => s.Id == cmd.Id, ct);
        if (server is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVER_NOT_FOUND);
        }

        var details = new ServerDetails(
            cmd.Name, cmd.Endpoint.Hostname, cmd.Endpoint.IpAddress, cmd.Endpoint.AssignedIpAddresses,
            cmd.Module, cmd.Username, cmd.Credentials, cmd.Endpoint.UseSSL,
            cmd.Limits.MaxAccounts, cmd.Limits.IsDefault, cmd.Limits.IsDisabled,
            cmd.Hosting.MonthlyCost, cmd.Hosting.Datacenter, cmd.Hosting.ServerStatusAddress,
            cmd.NameServers, cmd.Endpoint.AcceptsSelfSignedCertificate);

        server.Reconfigure(details, cmd.Clears ?? ServerCredentialClears.None);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
