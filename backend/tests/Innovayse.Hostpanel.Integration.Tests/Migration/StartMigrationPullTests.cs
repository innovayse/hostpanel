using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Commands.RunMigrationPull;
using Innovayse.Hostpanel.Application.Migration.Commands.StartMigrationPull;
using Innovayse.Hostpanel.Application.Migration.Common;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Migration;

/// <summary>
/// <c>POST /api/admin/migrations/{id}/start</c>, dispatching <c>StartMigrationPullCommand</c>:
/// starts pulling data from the legacy system into this panel, in the background.
/// </summary>
/// <remarks>
/// Runs in CI, where Testcontainers can start PostgreSQL; on a laptop without a reachable docker
/// socket every test in this project fails before the API boots, and that is the environment, not
/// these tests.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class StartMigrationPullTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Starting a pending job launches it and answers with the job's (still pending) state.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StartOnAPendingJobLaunchesThePull()
    {
        var client = await MigrationTestSeeds.AdminClientAsync(factory);
        var id = await MigrationTestSeeds.CreateJobAsync(client, MigrationTestSeeds.UnreachableSourceUrl);

        var response = await client.PostAsync($"/api/admin/migrations/{id}/start", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("id").GetInt32().Should().Be(id);
    }

    /// <summary>A job that is not Pending refuses to be started again.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StartRefusesAJobThatIsNotPending()
    {
        var client = await MigrationTestSeeds.AdminClientAsync(factory);
        var id = await MigrationTestSeeds.CreateJobAsync(client, MigrationTestSeeds.UnreachableSourceUrl);

        // Flipped directly through the real data context rather than by racing the background
        // worker this endpoint itself launches: the point under test is the status guard, not
        // how quickly a real pull reaches InProgress.
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var job = await context.MigrationJobs.FirstAsync(j => j.Id == id);
            job.Queue(DateTimeOffset.UtcNow);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var response = await client.PostAsync($"/api/admin/migrations/{id}/start", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await MigrationTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.MIGRATION_JOB_NOT_PENDING);
    }

    /// <summary>
    /// Two starts of the same pending job at once queue it once and run one pull: the job row's
    /// concurrency token lets only the first save win, and only that request publishes the pull.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TwoConcurrentStartsRunOneWorker()
    {
        var source = new FakeMigrationPullSource();
        source.SetDatasetJson("announcements", """[{"Title":"once","Content":"Body","Published":true}]""");
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        int id;
        using (var seedScope = host.Services.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var job = MigrationJob.Create(new MigrationJobSetup(
                "concurrent", "https://source.example.test",
                new MigrationExportSelection(
                    false, false, false, false, false, false, false, false, false, false, false, false, true, false, false)));
            seedContext.MigrationJobs.Add(job);
            await seedContext.SaveChangesAsync(CancellationToken.None);
            id = job.Id;
        }

        var bus = host.Services.GetRequiredService<IMessageBus>();
        var session = await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync(async Task (IMessageContext _) =>
            {
                var outcomes = await Task.WhenAll(
                    InvokeStartAsync(bus, id),
                    InvokeStartAsync(bus, id));
                outcomes.Count(o => o.IsSuccess).Should().Be(1);
                outcomes.Count(o => o.ErrorCode == HostpanelErrorCodes.MIGRATION_JOB_NOT_PENDING).Should().Be(1);
            });

        session.Executed.MessagesOf<RunMigrationPullCommand>().Should().ContainSingle();
        source.FetchCountFor("announcements").Should().Be(1);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.MigrationJobs.AsNoTracking().SingleAsync(j => j.Id == id)).Status
            .Should().Be(MigrationJobStatus.Completed);
    }

    /// <summary>An id naming no job is refused as missing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StartRefusesAnUnknownId()
    {
        var client = await MigrationTestSeeds.AdminClientAsync(factory);
        const int missing = 987654321;

        var response = await client.PostAsync($"/api/admin/migrations/{missing}/start", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await MigrationTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.MIGRATION_JOB_NOT_FOUND);
    }

    /// <summary>Dispatches one start in a scope of its own, as one HTTP request would.</summary>
    /// <param name="bus">The host's bus.</param>
    /// <param name="id">The job to start.</param>
    /// <returns>The start's outcome.</returns>
    private static Task<Result<MigrationJobDto>> InvokeStartAsync(IMessageBus bus, int id) =>
        bus.InvokeAsync<Result<MigrationJobDto>>(new StartMigrationPullCommand(id));

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StartRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsync("/api/admin/migrations/1/start", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
