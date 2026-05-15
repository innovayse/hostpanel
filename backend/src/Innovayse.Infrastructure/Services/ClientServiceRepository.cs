namespace Innovayse.Infrastructure.Services;

using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IClientServiceRepository"/>.</summary>
public sealed class ClientServiceRepository(AppDbContext db) : IClientServiceRepository
{
    /// <inheritdoc/>
    public async Task<ClientService?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.ClientServices.FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ClientService>> ListByClientAsync(int clientId, CancellationToken ct) =>
        await db.ClientServices
            .Where(s => s.ClientId == clientId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<ClientService> Items, int TotalCount)> ListAsync(
        int page, int pageSize, CancellationToken ct)
    {
        var total = await db.ClientServices.CountAsync(ct);
        var items = await db.ClientServices
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<ClientService> Items, int TotalCount)> ListAsync(
        int page, int pageSize, int? clientId, CancellationToken ct)
    {
        var query = db.ClientServices.AsQueryable();
        if (clientId.HasValue)
        {
            query = query.Where(s => s.ClientId == clientId.Value);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    /// <inheritdoc/>
    public void Add(ClientService service) => db.ClientServices.Add(service);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ClientService>> GetAllAsync(CancellationToken ct) =>
        await db.ClientServices.ToListAsync(ct);
}
