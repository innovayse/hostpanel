using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Commands.CreateServer;

/// <summary>
/// Handles <see cref="CreateServerCommand"/> by creating and persisting a new provisioning server.
/// </summary>
public sealed class CreateServerHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock that dates the new server.</summary>
    private readonly IClock _clock;

    /// <summary>The control panel modules a server can be given.</summary>
    private readonly IServerModuleCatalog _modules;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock that dates the new server.</param>
    /// <param name="modules">The control panel modules a server can be given.</param>
    public CreateServerHandler(IHostpanelDbContext context, IClock clock, IServerModuleCatalog modules)
    {
        _context = context;
        _clock = clock;
        _modules = modules;
    }

    /// <summary>
    /// Creates the server and returns its assigned identifier.
    /// </summary>
    /// <param name="cmd">The server's configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new server's identifier, or <c>SERVER_MODULE_UNKNOWN</c> when neither the host nor an installed plugin knows the module.</returns>
    public async Task<Result<int>> HandleAsync(CreateServerCommand cmd, CancellationToken ct)
    {
        var module = _modules.Canonical(cmd.Module);
        if (module is null)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.SERVER_MODULE_UNKNOWN);
        }

        var details = new ServerDetails(
            cmd.Name, cmd.Endpoint.Hostname, cmd.Endpoint.IpAddress, cmd.Endpoint.AssignedIpAddresses,
            module, cmd.Username, cmd.Credentials, cmd.Endpoint.UseSSL,
            cmd.Limits.MaxAccounts, cmd.Limits.IsDefault, cmd.Limits.IsDisabled,
            cmd.Hosting.MonthlyCost, cmd.Hosting.Datacenter, cmd.Hosting.ServerStatusAddress,
            cmd.NameServers, cmd.Endpoint.AcceptsSelfSignedCertificate);

        var server = Server.Create(details, _clock.UtcNow);
        _context.Servers.Add(server);
        await _context.SaveChangesAsync(ct);
        return Result<int>.Success(server.Id);
    }
}
