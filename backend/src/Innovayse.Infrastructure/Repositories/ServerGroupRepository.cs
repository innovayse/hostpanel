namespace Innovayse.Infrastructure.Repositories;
using Innovayse.Domain.Servers;
using Innovayse.Domain.Servers.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of <see cref="IServerGroupRepository"/>.
/// </summary>
public sealed class ServerGroupRepository(AppDbContext db) : IServerGroupRepository
{
    /// <inheritdoc/>
    public async Task<ServerGroup?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.ServerGroups
            .Include(g => g.Servers)
            .FirstOrDefaultAsync(g => g.Id == id, ct);

    /// <inheritdoc/>
    public async Task<List<ServerGroup>> ListAsync(CancellationToken ct) =>
        await db.ServerGroups
            .Include(g => g.Servers)
            .OrderBy(g => g.Name)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public void Add(ServerGroup group) => db.ServerGroups.Add(group);

    /// <inheritdoc/>
    public void Remove(ServerGroup group) => db.ServerGroups.Remove(group);
}
