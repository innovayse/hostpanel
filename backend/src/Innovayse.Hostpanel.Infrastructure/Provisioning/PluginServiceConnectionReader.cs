using Innovayse.Hostpanel.Application.Admin.Servers.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Provisioning;

/// <summary>
/// Default <see cref="IPluginServiceConnectionReader"/>: one read-only query over the service and its assigned server.
/// The server's password is read here and nowhere else, so no other consumer of the server connection holds it.
/// </summary>
public sealed class PluginServiceConnectionReader : IPluginServiceConnectionReader
{
    /// <summary>Data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the reader.</summary>
    /// <param name="context">Data context.</param>
    public PluginServiceConnectionReader(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<PluginServiceConnection?> ResolveAsync(int serviceId, CancellationToken ct)
    {
        // Read-only, and a left join: a service with no server, or whose server was deleted, still answers so the
        // caller can tell "missing" from "not usable".
        var row = await (
                from service in _context.ClientServices.AsNoTracking()
                where service.Id == serviceId
                join assigned in _context.Servers.AsNoTracking()
                    on service.ServerId equals (int?)assigned.Id into servers
                from server in servers.DefaultIfEmpty()
                select new { Service = service, Server = server })
            .FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }

        var access = row.Server is null
            ? null
            : new PluginServerAccess(row.Server.ToConnection(), row.Server.Password);
        return new PluginServiceConnection(
            row.Service.Id,
            row.Service.ClientId,
            row.Service.ProductId,
            row.Service.Status,
            row.Service.ProvisioningRef,
            access);
    }
}
