namespace Innovayse.Application.Migration.Commands.PingMigrationJob;

using Innovayse.Application.Migration.DTOs;
using Innovayse.Domain.Migration.Interfaces;

/// <summary>Handles <see cref="PingMigrationJobCommand"/>.</summary>
public sealed class PingMigrationJobHandler(IMigrationJobRepository repo)
{
    /// <summary>Records the plugin ping and returns the job config so the plugin knows what to export.</summary>
    public async Task<MigrationJobDto> HandleAsync(PingMigrationJobCommand cmd, CancellationToken ct)
    {
        var job = await repo.GetByKeyAsync(cmd.Key, ct)
            ?? throw new InvalidOperationException("Invalid migration key.");

        job.RecordPing();
        await repo.SaveAsync(ct);
        return job.ToDto();
    }
}
