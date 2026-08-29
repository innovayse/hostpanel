namespace Innovayse.Application.Migration.Queries.TestMigrationConnection;

using Innovayse.Application.Migration.Common;
using Innovayse.Application.Migration.Extensions;
using Innovayse.Application.Migration.Interfaces;
using Innovayse.Domain.Migration.Interfaces;

/// <summary>Pings the migration plugin at SourceUrl to verify connectivity.</summary>
public sealed class TestMigrationConnectionHandler(
    IMigrationJobRepository repo,
    IMigrationSource source)
{
    /// <summary>Sends a ping to the plugin and records the result.</summary>
    public async Task<MigrationJobDto> HandleAsync(TestMigrationConnectionQuery query, CancellationToken ct)
    {
        var job = await repo.GetByIdAsync(query.Id, ct)
            ?? throw new InvalidOperationException("Migration job not found.");

        await source.PingAsync(job.SourceUrl, job.Key, ct);

        job.RecordPing();
        await repo.SaveAsync(ct);
        return job.ToDto();
    }
}
