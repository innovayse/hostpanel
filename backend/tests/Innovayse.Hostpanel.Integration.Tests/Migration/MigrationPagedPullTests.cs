using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Pull;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Migration;

/// <summary>
/// A pull over several pages from a source that pages by keyset, as the WHMCS addon does: a page
/// that fails part way through fails the job but keeps the pages already imported, and a retry
/// finishes the job without importing those records a second time.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class MigrationPagedPullTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>How many records the source holds: more than one page of the pager's 50.</summary>
    private const int RecordCount = 60;

    /// <summary>The id the second page starts after, given the pager's page size of 50.</summary>
    private const long SecondPageStart = 50;

    /// <summary>
    /// The second page fails: the job fails as refused by the source, the first page's 50 records
    /// are imported and counted, and nothing of the second page is.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task SecondPageFails_JobFails_FirstPageIsKeptAsync()
    {
        var marker = Guid.NewGuid().ToString("N");
        var source = new PagedMigrationPullSource("announcements", Announcements(marker)) { FailOnceAfterId = SecondPageStart };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Announcements);

        await scope.ServiceProvider.GetRequiredService<MigrationPullWorker>().RunAsync(job.Id, CancellationToken.None);

        var reloaded = await context.MigrationJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal(MigrationJobStatus.Failed, reloaded.Status);
        Assert.Equal(MigrationJobFailure.SourceRefused, reloaded.Failure);
        Assert.Equal(50, reloaded.AnnouncementsImported);
        Assert.Equal(50, await context.Announcements.AsNoTracking().CountAsync(a => a.Title.StartsWith(marker)));
        Assert.Equal([0, SecondPageStart], source.RequestedAfterIds);
    }

    /// <summary>
    /// A retry after the failed page reads every page from the start: the 50 records imported
    /// before are skipped as already there, the last 10 are imported, and each record exists once.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task RetryAfterAFailedPage_ImportsTheRestOnceAsync()
    {
        var marker = Guid.NewGuid().ToString("N");
        var source = new PagedMigrationPullSource("announcements", Announcements(marker)) { FailOnceAfterId = SecondPageStart };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Announcements);
        await worker.RunAsync(job.Id, CancellationToken.None);

        var failed = await context.MigrationJobs.SingleAsync(j => j.Id == job.Id);
        failed.Queue(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);
        await worker.RunAsync(job.Id, CancellationToken.None);

        var reloaded = await context.MigrationJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal(MigrationJobStatus.Completed, reloaded.Status);
        Assert.Equal(10, reloaded.AnnouncementsImported);
        Assert.Equal(50, reloaded.AnnouncementsSkipped);
        Assert.Equal(0, reloaded.AnnouncementsFailed);
        Assert.Equal(RecordCount, await context.Announcements.AsNoTracking().CountAsync(a => a.Title.StartsWith(marker)));
    }

    /// <summary>A completed pull run a second time imports nothing new: every record is skipped as already there.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task SecondJobOverTheSameData_SkipsEveryRecordAsync()
    {
        var marker = Guid.NewGuid().ToString("N");
        var source = new PagedMigrationPullSource("announcements", Announcements(marker));
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        var first = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Announcements);
        await worker.RunAsync(first.Id, CancellationToken.None);

        var second = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Announcements);
        await worker.RunAsync(second.Id, CancellationToken.None);

        var reloaded = await context.MigrationJobs.AsNoTracking().SingleAsync(j => j.Id == second.Id);
        Assert.Equal(MigrationJobStatus.Completed, reloaded.Status);
        Assert.Equal(0, reloaded.AnnouncementsImported);
        Assert.Equal(RecordCount, reloaded.AnnouncementsSkipped);
        Assert.Equal(RecordCount, await context.Announcements.AsNoTracking().CountAsync(a => a.Title.StartsWith(marker)));
    }

    /// <summary>Builds the source's announcements, each with a unique title under the marker.</summary>
    /// <param name="marker">A prefix unique to the test, so its rows can be counted in the shared database.</param>
    /// <returns>One JSON object per announcement.</returns>
    private static List<string> Announcements(string marker) =>
        [.. Enumerable.Range(1, RecordCount).Select(i =>
            $$"""{"Title":"{{marker}}-{{i:D3}}","Content":"Body {{i}}","Published":true}""")];
}
