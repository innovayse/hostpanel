namespace Innovayse.Infrastructure.Persistence.Repositories;

using Innovayse.Domain.Migration;
using Innovayse.Domain.Migration.Interfaces;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IMigrationLogRepository"/>.</summary>
public sealed class MigrationLogRepository(AppDbContext db) : IMigrationLogRepository
{
    /// <inheritdoc/>
    public void Add(MigrationLog log) => db.MigrationLogs.Add(log);

    /// <inheritdoc/>
    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<MigrationLog> Items, int TotalCount)> ListByJobAsync(
        int jobId,
        MigrationLogAction? action,
        MigrationEntityType? entityType,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = db.MigrationLogs.Where(l => l.JobId == jobId);

        if (action.HasValue)
        {
            query = query.Where(l => l.Action == action.Value);
        }

        if (entityType.HasValue)
        {
            query = query.Where(l => l.EntityType == entityType.Value);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
