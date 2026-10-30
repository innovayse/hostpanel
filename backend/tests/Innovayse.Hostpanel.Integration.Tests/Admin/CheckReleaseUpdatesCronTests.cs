using Innovayse.Hostpanel.Application.Admin.Commands.CheckReleaseUpdatesCron;
using Microsoft.Extensions.Configuration;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Admin;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// <see cref="CheckReleaseUpdatesCronHandler"/>: a successful run stores the GitHub release in the
/// database and schedules the next daily run; a failed run still schedules the next run; and when
/// the check is disabled the GitHub client is never called but the next run is still scheduled.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CheckReleaseUpdatesCronTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    /// <summary>Clears the <c>LatestReleases</c> table so each case starts from a clean slate.</summary>
    /// <returns>A task.</returns>
    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await context.LatestReleases.ExecuteDeleteAsync();

        // Reset the fake between tests, including the call counter.
        factory.Services.GetRequiredService<FakeGitHubReleasesClient>().Reset();
    }

    /// <summary>Nothing to clean up after a case.</summary>
    /// <returns>A completed task.</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// A successful run stores the GitHub release as the singleton <see cref="LatestRelease"/> row
    /// and schedules one next daily run.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task SuccessfulRun_StoresTheLatestReleaseAndSchedulesNextRunAsync()
    {
        var fake = factory.Services.GetRequiredService<FakeGitHubReleasesClient>();
        fake.Next = new GitHubReleaseInfo(
            "2.0.0",
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            "https://github.com/innovayse/hostpanel/releases/tag/v2.0.0");

        var host = factory.Services.GetRequiredService<IHost>();
        var session = await host.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new CheckReleaseUpdatesCronCommand());

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var stored = await context.LatestReleases
            .SingleOrDefaultAsync(r => r.Id == LatestRelease.SingletonId);

        stored.Should().NotBeNull();
        stored!.Version.Should().Be("2.0.0");
        stored.ReleaseNotesUrl.Should().Contain("v2.0.0");

        NextRunsOf(session, typeof(CheckReleaseUpdatesCronCommand)).Should().Be(1);
    }

    /// <summary>
    /// When GitHub returns no published release, nothing is stored but the next run is still
    /// scheduled so the chain does not break.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task WhenGitHubReturnsNoRelease_NothingIsStoredAndNextRunIsScheduledAsync()
    {
        var fake = factory.Services.GetRequiredService<FakeGitHubReleasesClient>();
        fake.Next = null; // simulate GitHub returning no published release

        var host = factory.Services.GetRequiredService<IHost>();
        var session = await host.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new CheckReleaseUpdatesCronCommand());

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var stored = await context.LatestReleases.AnyAsync();
        stored.Should().BeFalse();

        NextRunsOf(session, typeof(CheckReleaseUpdatesCronCommand)).Should().Be(1);
    }

    /// <summary>
    /// A second successful run updates the stored row in place rather than inserting a second one.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task SecondRun_UpdatesTheExistingRowAsync()
    {
        var fake = factory.Services.GetRequiredService<FakeGitHubReleasesClient>();
        fake.Next = new GitHubReleaseInfo(
            "1.0.0",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            "https://github.com/innovayse/hostpanel/releases/tag/v1.0.0");

        var host = factory.Services.GetRequiredService<IHost>();
        await host.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new CheckReleaseUpdatesCronCommand());

        fake.Next = new GitHubReleaseInfo(
            "1.1.0",
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            "https://github.com/innovayse/hostpanel/releases/tag/v1.1.0");

        await host.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new CheckReleaseUpdatesCronCommand());

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var count = await context.LatestReleases.CountAsync();
        var stored = await context.LatestReleases.SingleAsync(r => r.Id == LatestRelease.SingletonId);

        count.Should().Be(1, "the singleton row is updated, not duplicated");
        stored.Version.Should().Be("1.1.0");
    }

    /// <summary>
    /// When <c>DisableReleaseUpdateChecks</c> is <see langword="true"/>, the GitHub client is
    /// never called and no row is written, but the next run is still scheduled.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task WhenChecksAreDisabled_GitHubIsNotCalledAndNextRunIsScheduledAsync()
    {
        var fake = factory.Services.GetRequiredService<FakeGitHubReleasesClient>();

        var host = factory.WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ScheduledJobs:DisableReleaseUpdateChecks"] = "true"
                })))
            .Services.GetRequiredService<IHost>();

        var session = await host.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new CheckReleaseUpdatesCronCommand());

        fake.CallCount.Should().Be(0, "the GitHub client must not be called when checks are disabled");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.LatestReleases.AnyAsync()).Should().BeFalse();

        NextRunsOf(session, typeof(CheckReleaseUpdatesCronCommand)).Should().Be(1);
    }

    /// <summary>
    /// A run that fails (the GitHub client throws) still schedules the next run exactly once, so
    /// the daily chain does not break.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task FailedRun_StillSchedulesTheNextRunOnceAsync()
    {
        var fake = factory.Services.GetRequiredService<FakeGitHubReleasesClient>();
        fake.ShouldFail = true;

        var host = factory.Services.GetRequiredService<IHost>();
        var session = await host.TrackActivity()
            .DoNotAssertOnExceptionsDetected()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new CheckReleaseUpdatesCronCommand());

        NextRunsOf(session, typeof(CheckReleaseUpdatesCronCommand))
            .Should().Be(1, "the next run is scheduled once even when this run failed");
    }

    /// <summary>
    /// Counts the runs of a job sent during the session other than the one being handled.
    /// Mirrors the helper in <see cref="Innovayse.Hostpanel.Integration.Tests.Shared.ScheduledJobsSurviveFailureTests"/>.
    /// </summary>
    /// <param name="session">The tracked session.</param>
    /// <param name="jobType">The job's command type.</param>
    /// <returns>How many next runs were sent.</returns>
    private static int NextRunsOf(ITrackedSession session, Type jobType)
    {
        var handled = session.AllRecordsInOrder()
            .FirstOrDefault(r => r.MessageEventType == MessageEventType.ExecutionStarted
                                 && r.Envelope?.Message?.GetType() == jobType)
            ?.Envelope?.Id;

        return session.Sent.Envelopes()
            .Where(e => e.Message?.GetType() == jobType && e.Id != handled)
            .Select(e => e.Id)
            .Distinct()
            .Count();
    }
}
