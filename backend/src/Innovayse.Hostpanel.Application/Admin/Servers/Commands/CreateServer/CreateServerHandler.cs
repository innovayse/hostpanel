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

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock that dates the new server.</param>
    public CreateServerHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>
    /// Creates the server and returns its assigned identifier.
    /// </summary>
    /// <param name="cmd">The server's configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new server's identifier. Always succeeds once validation has passed.</returns>
    public async Task<Result<int>> HandleAsync(CreateServerCommand cmd, CancellationToken ct)
    {
        var details = new ServerDetails(
            cmd.Name, cmd.Endpoint.Hostname, cmd.Endpoint.IpAddress, cmd.Endpoint.AssignedIpAddresses,
            cmd.Module, cmd.Username, cmd.Credentials, cmd.Endpoint.UseSSL,
            cmd.Limits.MaxAccounts, cmd.Limits.IsDefault, cmd.Limits.IsDisabled,
            cmd.Hosting.MonthlyCost, cmd.Hosting.Datacenter, cmd.Hosting.ServerStatusAddress,
            cmd.NameServers, cmd.Endpoint.AcceptsSelfSignedCertificate);

        var server = Server.Create(details, _clock.UtcNow);
        _context.Servers.Add(server);
        await _context.SaveChangesAsync(ct);
        return Result<int>.Success(server.Id);
    }
}
