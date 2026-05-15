namespace Innovayse.Infrastructure.Repositories;
using Innovayse.Domain.Servers;
using Innovayse.Domain.Servers.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of <see cref="IServerRepository"/>.
/// </summary>
public sealed class ServerRepository(AppDbContext db) : IServerRepository
{
    /// <inheritdoc/>
    public async Task<Server?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.Servers.FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <inheritdoc/>
    public async Task<List<Server>> ListAsync(ServerModule? module, CancellationToken ct)
    {
        var query = db.Servers.AsQueryable();
        if (module.HasValue)
        {
            query = query.Where(s => s.Module == module.Value);
        }

        return await query.OrderBy(s => s.Name).ToListAsync(ct);
    }

    /// <inheritdoc/>
    public void Add(Server server) => db.Servers.Add(server);

    /// <inheritdoc/>
    public void Remove(Server server) => db.Servers.Remove(server);
}
