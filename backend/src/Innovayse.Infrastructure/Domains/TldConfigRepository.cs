namespace Innovayse.Infrastructure.Domains;

using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="ITldConfigRepository"/>.</summary>
public sealed class TldConfigRepository(AppDbContext db) : ITldConfigRepository
{
    /// <inheritdoc/>
    public async Task<TldConfig?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.TldConfigs.FirstOrDefaultAsync(x => x.Id == id, ct);

    /// <inheritdoc/>
    public async Task<TldConfig?> FindByTldAsync(string tld, CancellationToken ct) =>
        await db.TldConfigs.FirstOrDefaultAsync(x => x.Tld == tld.ToLower().Trim(), ct);

    /// <inheritdoc/>
    public async Task<List<TldConfig>> ListAllAsync(CancellationToken ct) =>
        await db.TldConfigs.OrderBy(x => x.SortOrder).ThenBy(x => x.Tld).ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<List<TldConfig>> ListEnabledAsync(CancellationToken ct) =>
        await db.TldConfigs.Where(x => x.IsEnabled).OrderBy(x => x.SortOrder).ThenBy(x => x.Tld).ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<List<TldConfig>> ListByRegistrarAsync(RegistrarModule module, CancellationToken ct) =>
        await db.TldConfigs.Where(x => x.RegistrarModule == module).ToListAsync(ct);

    /// <inheritdoc/>
    public void Add(TldConfig config) => db.TldConfigs.Add(config);

    /// <inheritdoc/>
    public void Remove(TldConfig config) => db.TldConfigs.Remove(config);
}
