namespace Innovayse.Application.Migration.Commands.DeleteMigrationJob;

using Innovayse.Domain.Migration.Interfaces;

/// <summary>Handles <see cref="DeleteMigrationJobCommand"/>.</summary>
public sealed class DeleteMigrationJobHandler(IMigrationJobRepository repo)
{
    /// <summary>Deletes the migration job. Throws if not found.</summary>
    public async Task HandleAsync(DeleteMigrationJobCommand cmd, CancellationToken ct)
    {
        var job = await repo.GetByIdAsync(cmd.JobId, ct)
            ?? throw new InvalidOperationException($"Migration job {cmd.JobId} not found.");

        await repo.DeleteAsync(job, ct);
        await repo.SaveAsync(ct);
    }
}
