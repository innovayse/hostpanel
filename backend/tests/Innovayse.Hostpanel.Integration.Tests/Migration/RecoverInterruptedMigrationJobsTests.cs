using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Commands.RecoverInterruptedMigrationJobs;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Migration;

/// <summary>
/// <c>RecoverInterruptedMigrationJobsCommand</c>, the recurring recovery: queued and in-progress jobs
/// whose heartbeat is stale are failed as interrupted so they can be retried; a job with a fresh
/// heartbeat, which another instance may still be pulling, and every other job are untouched.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RecoverInterruptedMigrationJobsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the seeded rows and the events are stamped with.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// With the clock fixed, stale queued, running and heartbeat-less jobs fail as interrupted, fresh
    /// ones are kept, and a pending job stays pending.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task StaleJobs_AreFailed_FreshJobsAreKeptAsync()
    {
        using var host = new ClockedHost(factory, SeededAt);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var stale = scope.ServiceProvider.GetRequiredService<IOptions<ScheduledJobsOptions>>().Value.MigrationStaleAfter;
        var now = host.Clock.GetUtcNow();

        var staleQueued = await SeedJobAsync(context, running: false, heartbeatAt: now - stale - TimeSpan.FromSeconds(1));
        var staleRunning = await SeedJobAsync(context, running: true, heartbeatAt: now - stale - TimeSpan.FromMinutes(5));
        var freshQueued = await SeedJobAsync(context, running: false, heartbeatAt: now - stale + TimeSpan.FromSeconds(1));
        var freshRunning = await SeedJobAsync(context, running: true, heartbeatAt: now);
        var neverBeat = await SeedJobAsync(context, running: true, heartbeatAt: now);
        await ((DbContext)context).Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "MigrationJobs" SET "LastHeartbeatAt" = NULL WHERE "Id" = {neverBeat.Id}""", CancellationToken.None);
        var pending = MigrationJob.Create(new MigrationJobSetup(
            "pending", "https://source.example.test",
            new MigrationExportSelection(true, false, false, false, false, false, false, false, false, false, false, false, false, false, false)));
        context.MigrationJobs.Add(pending);
        await context.SaveChangesAsync(CancellationToken.None);

        await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(new RecoverInterruptedMigrationJobsCommand());

        var jobs = await context.MigrationJobs.AsNoTracking()
            .Where(j => new[] { staleQueued.Id, staleRunning.Id, freshQueued.Id, freshRunning.Id, neverBeat.Id, pending.Id }.Contains(j.Id))
            .ToDictionaryAsync(j => j.Id);
        Assert.Equal(MigrationJobStatus.Failed, jobs[staleQueued.Id].Status);
        Assert.Equal(MigrationJobFailure.Interrupted, jobs[staleQueued.Id].Failure);
        Assert.Equal(MigrationJobStatus.Failed, jobs[staleRunning.Id].Status);
        Assert.Equal(MigrationJobFailure.Interrupted, jobs[staleRunning.Id].Failure);
        Assert.Equal(MigrationJobStatus.Failed, jobs[neverBeat.Id].Status);
        Assert.Equal(MigrationJobFailure.Interrupted, jobs[neverBeat.Id].Failure);
        Assert.Equal(MigrationJobStatus.Queued, jobs[freshQueued.Id].Status);
        Assert.Equal(MigrationJobStatus.InProgress, jobs[freshRunning.Id].Status);
        Assert.Equal(MigrationJobStatus.Pending, jobs[pending.Id].Status);
    }

    /// <summary>Seeds a queued or running job whose last heartbeat is the given moment.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="running">True to start the job (in progress); false to leave it queued.</param>
    /// <param name="heartbeatAt">The heartbeat the job carries.</param>
    /// <returns>The saved job.</returns>
    private static async Task<MigrationJob> SeedJobAsync(IHostpanelDbContext context, bool running, DateTimeOffset heartbeatAt)
    {
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Clients);
        if (running)
        {
            job.Start(MigrationTotals.None);
        }

        job.RecordHeartbeat(heartbeatAt);
        await context.SaveChangesAsync(CancellationToken.None);
        return job;
    }
}
