namespace Innovayse.Application.Migration.Queries.ListMigrationJobs;

using Innovayse.Application.Migration.DTOs;
using Innovayse.Domain.Migration.Interfaces;

/// <summary>Handles <see cref="ListMigrationJobsQuery"/>.</summary>
public sealed class ListMigrationJobsHandler(IMigrationJobRepository repo)
{
    /// <summary>Returns all migration jobs as DTOs, ordered by creation date descending.</summary>
    public async Task<IReadOnlyList<MigrationJobDto>> HandleAsync(ListMigrationJobsQuery query, CancellationToken ct)
    {
        var jobs = await repo.ListAsync(ct);
        return jobs.Select(j => j.ToDto()).ToList();
    }
}
