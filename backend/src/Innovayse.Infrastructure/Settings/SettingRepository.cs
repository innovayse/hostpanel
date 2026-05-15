namespace Innovayse.Infrastructure.Settings;

using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="ISettingRepository"/>.</summary>
/// <param name="db">The application database context.</param>
public sealed class SettingRepository(AppDbContext db) : ISettingRepository
{
    /// <inheritdoc/>
    public async Task<Setting?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.Settings.FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <inheritdoc/>
    public async Task<Setting?> FindByKeyAsync(string key, CancellationToken ct) =>
        await db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);

    /// <inheritdoc/>
    public void Add(Setting setting) => db.Settings.Add(setting);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Setting>> ListAsync(CancellationToken ct) =>
        await db.Settings
            .OrderBy(s => s.Key)
            .ToListAsync(ct);
}
