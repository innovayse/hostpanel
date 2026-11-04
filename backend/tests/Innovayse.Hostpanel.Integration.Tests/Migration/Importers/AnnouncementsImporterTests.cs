using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Common;
using Innovayse.Hostpanel.Application.Migration.Importers;
using Innovayse.Hostpanel.Application.Migration.Pull;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Migration.Importers;

/// <summary>
/// <see cref="AnnouncementsImporter"/>, driven end to end through
/// <c>MigrationPullWorker.RunAsync</c> against a <see cref="FakeMigrationSource"/>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AnnouncementsImporterTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Two distinct announcement records: <see cref="AnnouncementsImporter"/> creates both, keeping the
    /// source creation date of each.
    /// </summary>
    [Fact]
    public async Task RunAsync_Announcements_CreatesEveryDistinctRecordAsync()
    {
        var source = new FakeMigrationSource { Totals = MigrationSourceTotals.Empty with { Content = new MigrationContentTotals(Announcements: 2) } };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Announcements);
        var marker = Guid.NewGuid().ToString("N");
        source.SetDatasetJson("announcements", $$"""
            [
                {"Title":"{{marker}}-one","Content":"Body one","Published":true,"CreatedAt":"2025-01-01T00:00:00Z"},
                {"Title":"{{marker}}-two","Content":"Body two","Published":false,"CreatedAt":"2025-01-02T00:00:00Z"}
            ]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var announcements = await context.Announcements.AsNoTracking()
            .Where(a => a.Title.StartsWith(marker))
            .ToListAsync();
        Assert.Equal(2, announcements.Count);

        var reloaded = await context.MigrationJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal(Domain.Migration.Enums.MigrationJobStatus.Completed, reloaded.Status);
    }

    /// <summary>Runs one pull of the announcements dataset as a fresh job on the given scope and returns the job.</summary>
    /// <param name="scope">The scope whose context and worker run the pull.</param>
    /// <returns>The reloaded job after the pull finished.</returns>
    private static async Task<Domain.Migration.MigrationJob> PullAsync(IServiceScope scope)
    {
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Announcements);
        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);
        return await context.MigrationJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
    }

    /// <summary>Builds a source holding the given announcements dataset.</summary>
    /// <param name="json">The dataset's single page as a JSON array.</param>
    /// <returns>The seeded fake source.</returns>
    private static FakeMigrationSource SourceWith(string json)
    {
        var source = new FakeMigrationSource { Totals = MigrationSourceTotals.Empty with { Content = new MigrationContentTotals(Announcements: 1) } };
        source.SetDatasetJson("announcements", json);
        return source;
    }

    /// <summary>A second run of the same dataset creates no second row: the retry imports 0 and skips every record as already existing.</summary>
    [Fact]
    public async Task RunAsync_Announcements_SecondRunSkipsAsAlreadyExistsAsync()
    {
        var marker = Guid.NewGuid().ToString("N");
        var source = SourceWith($$"""[{"Title":"{{marker}}","Content":"Body","Published":true,"CreatedAt":"2025-01-01T00:00:00Z"}]""");
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        await PullAsync(scope);
        var second = await PullAsync(scope);

        Assert.Equal(1, await context.Announcements.AsNoTracking().CountAsync(a => a.Title == marker));
        Assert.Equal(0, second.AnnouncementsImported);
        Assert.Equal(1, second.AnnouncementsSkipped);
        var log = await context.MigrationLogs.AsNoTracking()
            .SingleAsync(l => l.JobId == second.Id && l.EntityType == MigrationEntityType.Announcements);
        Assert.Equal(MigrationSkipReason.AlreadyExists, log.SkipReason);
    }

    /// <summary>The title match ignores case, so a retry with differently cased text is still a duplicate.</summary>
    [Fact]
    public async Task RunAsync_Announcements_TitleMatchIgnoresCaseAsync()
    {
        var marker = Guid.NewGuid().ToString("N");
        var first = SourceWith($$"""[{"Title":"{{marker}}-Case","Content":"Body","Published":true,"CreatedAt":"2025-01-01T00:00:00Z"}]""");
        using (var host = MigrationTestSeeds.HostWithFakeSource(factory, first))
        {
            using var scope = host.Services.CreateScope();
            await PullAsync(scope);
        }

        var again = SourceWith($$"""[{"Title":"{{marker}}-CASE","Content":"Body","Published":true,"CreatedAt":"2025-01-01T00:00:00Z"}]""");
        using var secondHost = MigrationTestSeeds.HostWithFakeSource(factory, again);
        using var secondScope = secondHost.Services.CreateScope();
        var second = await PullAsync(secondScope);

        Assert.Equal(0, second.AnnouncementsImported);
        Assert.Equal(1, second.AnnouncementsSkipped);
    }

    /// <summary>The same title on a different date is a different announcement and is created.</summary>
    [Fact]
    public async Task RunAsync_Announcements_SameTitleOnADifferentDateIsCreatedAsync()
    {
        var marker = Guid.NewGuid().ToString("N");
        var source = SourceWith($$"""[{"Title":"{{marker}}","Content":"Body","Published":true,"CreatedAt":"2025-01-01T00:00:00Z"}]""");
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await PullAsync(scope);
        source.SetDatasetJson("announcements", $$"""[{"Title":"{{marker}}","Content":"Body","Published":true,"CreatedAt":"2025-02-01T00:00:00Z"}]""");

        var second = await PullAsync(scope);

        Assert.Equal(2, await context.Announcements.AsNoTracking().CountAsync(a => a.Title == marker));
        Assert.Equal(1, second.AnnouncementsImported);
        Assert.Equal(0, second.AnnouncementsSkipped);
    }
}
