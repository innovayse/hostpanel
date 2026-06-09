namespace Innovayse.Infrastructure.Persistence.Repositories;

using Innovayse.Domain.Migration;
using Innovayse.Domain.Migration.Interfaces;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IMigrationJobRepository"/>.</summary>
public sealed class MigrationJobRepository(AppDbContext db) : IMigrationJobRepository
{
    /// <inheritdoc/>
    public Task<MigrationJob?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.MigrationJobs.FirstOrDefaultAsync(j => j.Id == id, ct);

    /// <inheritdoc/>
    public Task<MigrationJob?> GetByKeyAsync(string key, CancellationToken ct = default) =>
        db.MigrationJobs.FirstOrDefaultAsync(j => j.Key == key, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MigrationJob>> ListAsync(CancellationToken ct = default) =>
        await db.MigrationJobs.OrderByDescending(j => j.CreatedAt).ToListAsync(ct);

    /// <inheritdoc/>
    public Task AddAsync(MigrationJob job, CancellationToken ct = default) =>
        db.MigrationJobs.AddAsync(job, ct).AsTask();

    /// <inheritdoc/>
    public Task DeleteAsync(MigrationJob job, CancellationToken ct = default)
    {
        db.MigrationJobs.Remove(job);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SaveAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
