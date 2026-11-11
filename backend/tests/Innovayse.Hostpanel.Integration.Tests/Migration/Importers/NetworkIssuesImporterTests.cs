using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Common;
using Innovayse.Hostpanel.Application.Migration.Importers;
using Innovayse.Hostpanel.Application.Migration.Pull;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Migration.Importers;

/// <summary>
/// <see cref="NetworkIssuesImporter"/>, driven end to end through
/// <c>MigrationPullWorker.RunAsync</c> against a <see cref="FakeMigrationSource"/>: creates a
/// network issue record and, on a retry, skips it as already existing (title and start date).
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class NetworkIssuesImporterTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A resolved network issue record is imported with its resolved status applied.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_NetworkIssues_ImportsAResolvedIssueAsync()
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Support = new MigrationSupportTotals(NetworkIssues: 1) } };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.NetworkIssues);
        var title = $"issue-{Guid.NewGuid():N}";
        source.SetDatasetJson("network_issues", $$"""
            [{"Title":"{{title}}","Type":"server","Status":"resolved","Priority":"high","Description":"Outage","StartDate":"2025-01-01T00:00:00Z","EndDate":"2025-01-02T00:00:00Z"}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var issue = await context.NetworkIssues.AsNoTracking().FirstOrDefaultAsync(i => i.Title == title);
        Assert.NotNull(issue);
    }

    /// <summary>Builds a source holding the given network issues dataset.</summary>
    /// <param name="json">The dataset's single page as a JSON array.</param>
    /// <returns>The seeded fake source.</returns>
    private static FakeMigrationSource SourceWith(string json)
    {
        var source = new FakeMigrationSource { Totals = MigrationSourceTotals.Empty with { Support = new MigrationSupportTotals(NetworkIssues: 1) } };
        source.SetDatasetJson("network_issues", json);
        return source;
    }

    /// <summary>Runs one pull of the network issues dataset as a fresh job and returns the reloaded job.</summary>
    /// <param name="scope">The scope whose context and worker run the pull.</param>
    /// <returns>The job after the pull finished.</returns>
    private static async Task<Domain.Migration.MigrationJob> PullAsync(IServiceScope scope)
    {
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.NetworkIssues);
        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);
        return await context.MigrationJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
    }

    /// <summary>A second run creates no second row: the retry imports 0 and skips the record as already existing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_NetworkIssues_SecondRunSkipsAsAlreadyExistsAsync()
    {
        var title = $"issue-{Guid.NewGuid():N}";
        var source = SourceWith($$"""[{"Title":"{{title}}","Type":"server","Status":"open","Priority":"high","Description":"Outage","StartDate":"2025-01-01T00:00:00Z","EndDate":null}]""");
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        await PullAsync(scope);
        var second = await PullAsync(scope);

        Assert.Equal(1, await context.NetworkIssues.AsNoTracking().CountAsync(i => i.Title == title));
        Assert.Equal(0, second.NetworkIssuesImported);
        Assert.Equal(1, second.NetworkIssuesSkipped);
        var log = await context.MigrationLogs.AsNoTracking()
            .SingleAsync(l => l.JobId == second.Id && l.EntityType == MigrationEntityType.NetworkIssues);
        Assert.Equal(MigrationSkipReason.AlreadyExists, log.SkipReason);
    }

    /// <summary>The same title with a different start date is a new incident and is created.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_NetworkIssues_SameTitleWithADifferentStartDateIsCreatedAsync()
    {
        var title = $"issue-{Guid.NewGuid():N}";
        var source = SourceWith($$"""[{"Title":"{{title}}","Type":"server","Status":"open","Priority":"high","Description":"Outage","StartDate":"2025-01-01T00:00:00Z","EndDate":null}]""");
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await PullAsync(scope);
        source.SetDatasetJson("network_issues", $$"""[{"Title":"{{title}}","Type":"server","Status":"open","Priority":"high","Description":"Outage","StartDate":"2025-03-01T00:00:00Z","EndDate":null}]""");

        var second = await PullAsync(scope);

        Assert.Equal(2, await context.NetworkIssues.AsNoTracking().CountAsync(i => i.Title == title));
        Assert.Equal(1, second.NetworkIssuesImported);
    }

    /// <summary>A record without a start date is matched by title alone, so a retry does not duplicate it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_NetworkIssues_UndatedRecordMatchesByTitleAloneAsync()
    {
        var title = $"issue-{Guid.NewGuid():N}";
        var source = SourceWith($$"""[{"Title":"{{title}}","Type":"other","Status":"open","Priority":"low","Description":"","StartDate":null,"EndDate":null}]""");
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        await PullAsync(scope);
        var second = await PullAsync(scope);

        Assert.Equal(1, await context.NetworkIssues.AsNoTracking().CountAsync(i => i.Title == title));
        Assert.Equal(0, second.NetworkIssuesImported);
        Assert.Equal(1, second.NetworkIssuesSkipped);
    }

    /// <summary>A source start date finer than a microsecond still matches on a retry, because it is stored at microsecond precision.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_NetworkIssues_SubMicrosecondStartDateIsStillDuplicateOnRetryAsync()
    {
        var title = $"issue-{Guid.NewGuid():N}";
        var source = SourceWith($$"""[{"Title":"{{title}}","Type":"server","Status":"open","Priority":"high","Description":"Outage","StartDate":"2025-01-01T00:00:00.1234567Z","EndDate":null}]""");
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        await PullAsync(scope);
        var second = await PullAsync(scope);

        Assert.Equal(1, await context.NetworkIssues.AsNoTracking().CountAsync(i => i.Title == title));
        Assert.Equal(0, second.NetworkIssuesImported);
        Assert.Equal(1, second.NetworkIssuesSkipped);
    }
}
