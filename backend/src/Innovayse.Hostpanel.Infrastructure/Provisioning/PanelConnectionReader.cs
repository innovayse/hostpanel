using Hostpanel.Sdk.Plugins.Provisioning;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Extensions;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Provisioning;

/// <summary>
/// Builds the connection a panel plugin is handed from the connection details every provider receives. The server's stored API
/// password is not part of those details, so it is read here, on the one path that gives it to a plugin, and goes nowhere else.
/// </summary>
public sealed class PanelConnectionReader
{
    /// <summary>The database the stored password is read from.</summary>
    private readonly AppDbContext _db;

    /// <summary>Initializes a new instance of the <see cref="PanelConnectionReader"/> class.</summary>
    /// <param name="db">The database the stored password is read from.</param>
    public PanelConnectionReader(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Reads the server's stored password and maps the details to the plugin's connection.</summary>
    /// <param name="server">The server's connection details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connection to put in a panel plugin context.</returns>
    public async Task<PanelServerConnection> ReadAsync(ServerConnectionDto server, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(server);

        var password = await _db.Servers.AsNoTracking()
            .Where(s => s.Id == server.Id)
            .Select(s => s.Password)
            .FirstOrDefaultAsync(ct);

        return server.ToPanelConnection(password);
    }
}
