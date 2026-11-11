using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Commands.RunMigrationPull;
using Innovayse.Hostpanel.Application.Migration.Common;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Migration;

/// <summary>
/// <c>RunMigrationPullCommand</c>, the background message that runs a queued job's pull: one bad
/// record is counted and logged as failed without failing the job, the log names records only by
/// position, and a job that is not queued is left alone.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RunMigrationPullTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the seeded rows and the events are stamped with.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A record whose save fails is discarded and counted as failed; the records after it still
    /// import, the job completes at 100 percent, and no log row carries a field of a record.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task FailedRecord_IsIsolatedAndCountedAsync()
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Content = new(Announcements: 3) } };
        var marker = Guid.NewGuid().ToString("N");
        var tooLong = new string('x', 600);
        source.SetDatasetJson("announcements", $$"""
            [
                {"Title":"{{marker}}-one","Content":"Body","Published":true},
                {"Title":"{{tooLong}}","Content":"Body","Published":true},
                {"Title":"{{marker}}-three","Content":"Body","Published":true}
            ]
            """);
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Announcements);

        await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(new RunMigrationPullCommand(job.Id));

        var reloaded = await context.MigrationJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal(MigrationJobStatus.Completed, reloaded.Status);
        Assert.Equal(2, reloaded.AnnouncementsImported);
        Assert.Equal(1, reloaded.AnnouncementsFailed);
        Assert.Equal(100, reloaded.OverallPercent());
        Assert.NotNull(reloaded.LastHeartbeatAt);
        Assert.Equal(2, await context.Announcements.AsNoTracking().CountAsync(a => a.Title.StartsWith(marker)));

        var logs = await context.MigrationLogs.AsNoTracking().Where(l => l.JobId == job.Id).OrderBy(l => l.Id).ToListAsync();
        Assert.Equal(["#1", "#2", "#3"], logs.Select(l => l.Identifier));
        Assert.Equal(MigrationLogAction.Failed, logs[1].Action);
    }

    /// <summary>A job that is not queued, such as one still pending, is not pulled.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task JobNotQueued_IsLeftAloneAsync()
    {
        var source = new FakeMigrationPullSource();
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Announcements);
        job.Fail(MigrationJobFailure.Interrupted, SeededAt);
        await context.SaveChangesAsync(CancellationToken.None);

        await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(new RunMigrationPullCommand(job.Id));

        var reloaded = await context.MigrationJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal(MigrationJobStatus.Failed, reloaded.Status);
        Assert.Equal(0, source.FetchCountFor("announcements"));
    }

    /// <summary>
    /// A pull message delivered again after its job completed (a redelivery after a restart) pulls
    /// nothing a second time: the source is not read again and no record is imported twice.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task CompletedJob_RedeliveredPull_ImportsNothingTwiceAsync()
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Content = new(Announcements: 1) } };
        var marker = Guid.NewGuid().ToString("N");
        source.SetDatasetJson("announcements", $$"""
            [ {"Title":"{{marker}}","Content":"Body","Published":true} ]
            """);
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Announcements);
        await bus.InvokeAsync(new RunMigrationPullCommand(job.Id));
        var fetchesAfterFirstPull = source.FetchCountFor("announcements");

        await bus.InvokeAsync(new RunMigrationPullCommand(job.Id));

        var reloaded = await context.MigrationJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal(MigrationJobStatus.Completed, reloaded.Status);
        Assert.Equal(fetchesAfterFirstPull, source.FetchCountFor("announcements"));
        Assert.Equal(1, await context.Announcements.AsNoTracking().CountAsync(a => a.Title == marker));
    }

    /// <summary>
    /// A pull message for a job whose row is gone, as after the demo reset truncated it, ends without
    /// throwing, so it is not retried, and reads nothing from the source.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task MissingJob_RedeliveredPull_EndsQuietlyAsync()
    {
        var source = new FakeMigrationPullSource();
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();

        var pull = () => scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(new RunMigrationPullCommand(int.MaxValue));

        await pull.Should().NotThrowAsync();
        Assert.Equal(0, source.FetchCountFor("announcements"));
    }

    /// <summary>
    /// Two pulls of one queued job run at once: the second starts and completes the job while the first
    /// is between reading the job and starting it. The first loses the start on the job's concurrency
    /// token and returns quietly, so the job ends completed, not failed, and each record is imported once.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ConcurrentPulls_LoserReturnsQuietly_JobCompletesAsync()
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Content = new(Announcements: 1) } };
        var marker = Guid.NewGuid().ToString("N");
        source.SetDatasetJson("announcements", $$"""
            [ {"Title":"{{marker}}","Content":"Body","Published":true} ]
            """);
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Announcements);
        source.BeforeFirstTotals = async () =>
        {
            // The competing pull runs in its own scope, so it has its own context and its own read of the job.
            using var competitor = host.Services.CreateScope();
            await competitor.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(new RunMigrationPullCommand(job.Id));
        };

        var pull = () => scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(new RunMigrationPullCommand(job.Id));

        await pull.Should().NotThrowAsync();
        Assert.Null(source.BeforeFirstTotals);
        var reloaded = await context.MigrationJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal(MigrationJobStatus.Completed, reloaded.Status);
        Assert.Null(reloaded.Failure);
        Assert.Equal(1, reloaded.AnnouncementsImported);
        Assert.Equal(1, source.FetchCountFor("announcements"));
        Assert.Equal(1, await context.Announcements.AsNoTracking().CountAsync(a => a.Title == marker));
    }
}
