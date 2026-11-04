using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Common;
using Innovayse.Hostpanel.Application.Migration.Importers;
using Innovayse.Hostpanel.Application.Migration.Pull;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Migration.Importers;

/// <summary>
/// <see cref="DownloadsImporter"/>, driven end to end through <c>MigrationPullWorker.RunAsync</c>
/// against a <see cref="FakeMigrationSource"/>: creates a download category, then a
/// downloadable file placed into it. A retry reuses the category by name and skips a file already
/// held (title and category) as already existing.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DownloadsImporterTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A category and a download file are both created.</summary>
    [Fact]
    public async Task RunAsync_Downloads_CreatesTheCategoryAndFileAsync()
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Content = new MigrationContentTotals(Downloads: 1) } };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Downloads);
        var marker = Guid.NewGuid().ToString("N");
        var title = $"download-{marker}";
        source.SetDatasetJson("download_categories", $$"""
            [{"Id":1,"Name":"category-{{marker}}","Description":null,"Hidden":false,"ParentId":null}]
            """);
        source.SetDatasetJson("downloads", $$"""
            [{"Title":"{{title}}","Description":null,"Type":"file","Filename":"file.zip","CategoryId":1,"ClientsOnly":false,"Hidden":false}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var download = await context.Downloads.AsNoTracking().FirstOrDefaultAsync(d => d.Title == title);
        Assert.NotNull(download);
        var category = await context.DownloadCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == download!.CategoryId);
        Assert.NotNull(category);
    }

    /// <summary>
    /// A file whose source holds no category at all has nowhere to go: it is skipped with
    /// <see cref="MigrationSkipReason.DownloadCategoryNotFound"/> and nothing is written.
    /// </summary>
    [Fact]
    public async Task RunAsync_Downloads_SkipsAFileWhenThereIsNoCategoryAsync()
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Content = new MigrationContentTotals(Downloads: 1) } };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Downloads);
        var title = $"download-{Guid.NewGuid():N}";
        source.SetDatasetJson("download_categories", "[]");
        source.SetDatasetJson("downloads", $$"""
            [{"Title":"{{title}}","Description":null,"Type":"file","Filename":"file.zip","CategoryId":null,"ClientsOnly":false,"Hidden":false}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        Assert.False(await context.Downloads.AsNoTracking().AnyAsync(d => d.Title == title));
        var log = await context.MigrationLogs.AsNoTracking()
            .SingleAsync(l => l.JobId == job.Id && l.EntityType == MigrationEntityType.Downloads);
        Assert.Equal(MigrationLogAction.Skipped, log.Action);
        Assert.Equal(MigrationSkipReason.DownloadCategoryNotFound, log.SkipReason);
    }

    /// <summary>Builds a source holding the given categories and downloads datasets.</summary>
    /// <param name="categoriesJson">The categories dataset's single page as a JSON array.</param>
    /// <param name="downloadsJson">The downloads dataset's single page as a JSON array.</param>
    /// <returns>The seeded fake source.</returns>
    private static FakeMigrationSource SourceWith(string categoriesJson, string downloadsJson)
    {
        var source = new FakeMigrationSource { Totals = MigrationSourceTotals.Empty with { Content = new MigrationContentTotals(Downloads: 1) } };
        source.SetDatasetJson("download_categories", categoriesJson);
        source.SetDatasetJson("downloads", downloadsJson);
        return source;
    }

    /// <summary>Runs one pull of the downloads datasets as a fresh job and returns the reloaded job.</summary>
    /// <param name="scope">The scope whose context and worker run the pull.</param>
    /// <returns>The job after the pull finished.</returns>
    private static async Task<Domain.Migration.MigrationJob> PullAsync(IServiceScope scope)
    {
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Downloads);
        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);
        return await context.MigrationJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
    }

    /// <summary>
    /// A second run reuses the category by name and skips the file as already existing: one category,
    /// one file, and the retry is never reported as <see cref="MigrationSkipReason.DownloadCategoryNotFound"/>.
    /// </summary>
    [Fact]
    public async Task RunAsync_Downloads_SecondRunSkipsAsAlreadyExistsAsync()
    {
        var marker = Guid.NewGuid().ToString("N");
        var source = SourceWith(
            $$"""[{"Id":1,"Name":"category-{{marker}}","Description":null,"Hidden":false,"ParentId":null}]""",
            $$"""[{"Title":"download-{{marker}}","Description":null,"Type":"file","Filename":"file.zip","CategoryId":1,"ClientsOnly":false,"Hidden":false}]""");
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        await PullAsync(scope);
        var second = await PullAsync(scope);

        Assert.Equal(1, await context.DownloadCategories.AsNoTracking().CountAsync(c => c.Name == $"category-{marker}"));
        Assert.Equal(1, await context.Downloads.AsNoTracking().CountAsync(d => d.Title == $"download-{marker}"));
        Assert.Equal(0, second.DownloadsImported);
        Assert.Equal(1, second.DownloadsSkipped);
        var log = await context.MigrationLogs.AsNoTracking()
            .SingleAsync(l => l.JobId == second.Id && l.EntityType == MigrationEntityType.Downloads);
        Assert.Equal(MigrationSkipReason.AlreadyExists, log.SkipReason);
    }

    /// <summary>The same title in two different categories is two files, not a duplicate.</summary>
    [Fact]
    public async Task RunAsync_Downloads_SameTitleInTwoCategoriesIsNotADuplicateAsync()
    {
        var marker = Guid.NewGuid().ToString("N");
        var source = SourceWith(
            $$"""[{"Id":1,"Name":"first-{{marker}}","Description":null,"Hidden":false,"ParentId":null},{"Id":2,"Name":"second-{{marker}}","Description":null,"Hidden":false,"ParentId":null}]""",
            $$"""[{"Title":"shared-{{marker}}","Description":null,"Type":"file","Filename":"a.zip","CategoryId":1,"ClientsOnly":false,"Hidden":false},{"Title":"shared-{{marker}}","Description":null,"Type":"file","Filename":"b.zip","CategoryId":2,"ClientsOnly":false,"Hidden":false}]""");
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var job = await PullAsync(scope);

        Assert.Equal(2, await context.Downloads.AsNoTracking().CountAsync(d => d.Title == $"shared-{marker}"));
        Assert.Equal(2, job.DownloadsImported);
        Assert.Equal(0, job.DownloadsSkipped);
    }
}
