namespace Innovayse.Application.Migration.Commands.StartMigrationPull;

using Innovayse.Application.Migration.DTOs;
using Innovayse.Application.Migration.Services;
using Innovayse.Domain.Migration;
using Innovayse.Domain.Migration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>Handles <see cref="StartMigrationPullCommand"/>: validates the job and fires a background pull.</summary>
public sealed class StartMigrationPullHandler(
    IMigrationJobRepository repo,
    IServiceScopeFactory scopeFactory,
    ILogger<StartMigrationPullHandler> logger)
{
    /// <summary>Validates the job exists and is pending, then starts the pull in the background.</summary>
    public async Task<MigrationJobDto> HandleAsync(StartMigrationPullCommand cmd, CancellationToken ct)
    {
        var job = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException("Migration job not found.");

        if (job.Status != MigrationJobStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot start a migration job with status '{job.Status}'.");
        }

        var jobId = job.Id;

        _ = Task.Run(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
            await worker.RunAsync(jobId, CancellationToken.None);
        });

        logger.LogInformation("Migration pull started in background for job {JobId}.", jobId);

        return job.ToDto();
    }
}
