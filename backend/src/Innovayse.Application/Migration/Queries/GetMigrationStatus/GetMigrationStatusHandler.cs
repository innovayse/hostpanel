namespace Innovayse.Application.Migration.Queries.GetMigrationStatus;

using Innovayse.Application.Migration.DTOs;
using Innovayse.Domain.Migration.Interfaces;

/// <summary>Handles <see cref="GetMigrationStatusQuery"/>.</summary>
public sealed class GetMigrationStatusHandler(IMigrationJobRepository repo)
{
    /// <summary>Returns the migration job DTO, or null if the job does not exist.</summary>
    public async Task<MigrationJobDto?> HandleAsync(GetMigrationStatusQuery query, CancellationToken ct)
    {
        var job = await repo.GetByIdAsync(query.JobId, ct);
        return job?.ToDto();
    }
}
