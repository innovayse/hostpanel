using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Admin;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Application.Admin.Commands.CheckReleaseUpdatesCron;

/// <summary>
/// Handles <see cref="CheckReleaseUpdatesCronCommand"/> by calling the GitHub Releases API,
/// storing the result in the <c>LatestReleases</c> table, and scheduling the next daily run.
/// Skips the GitHub call when <c>ScheduledJobs:DisableReleaseUpdateChecks</c> is <see langword="true"/>.
/// </summary>
public sealed class CheckReleaseUpdatesCronHandler
{
    /// <summary>The data context, for reading and writing the singleton <see cref="LatestRelease"/> row.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Fetches the latest published release from GitHub.</summary>
    private readonly IGitHubReleasesClient _github;

    /// <summary>Runs the pass under its lease and schedules the next run.</summary>
    private readonly RecurringJobs _jobs;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>The scheduled-jobs configuration, for the disable flag and the daily hour.</summary>
    private readonly IOptions<ScheduledJobsOptions> _options;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<CheckReleaseUpdatesCronHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context, for reading and writing the singleton <see cref="LatestRelease"/> row.</param>
    /// <param name="github">Fetches the latest published release from GitHub.</param>
    /// <param name="jobs">Runs the pass under its lease and schedules the next run.</param>
    /// <param name="clock">The clock every moment in this handler is read from.</param>
    /// <param name="options">The scheduled-jobs configuration, for the disable flag and the daily hour.</param>
    /// <param name="logger">Structured logger.</param>
    public CheckReleaseUpdatesCronHandler(
        IHostpanelDbContext context,
        IGitHubReleasesClient github,
        RecurringJobs jobs,
        IClock clock,
        IOptions<ScheduledJobsOptions> options,
        ILogger<CheckReleaseUpdatesCronHandler> logger)
    {
        _context = context;
        _github = github;
        _jobs = jobs;
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Runs the release-check pass: skips outbound HTTP when checks are disabled, otherwise
    /// calls GitHub and upserts the <see cref="LatestRelease"/> singleton row.
    /// Re-schedules itself for the next day after completion.
    /// </summary>
    /// <param name="cmd">The scheduled command (marker, no payload).</param>
    /// <param name="ct">Cancellation token.</param>
    public Task HandleAsync(CheckReleaseUpdatesCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;
        var now = _clock.UtcNow;

        return _jobs.RunAsync<CheckReleaseUpdatesCronCommand>(
            _jobs.Schedules.CheckReleaseUpdates, () => RunAsync(now, ct), ct);
    }

    /// <summary>One pass of the job; <see cref="HandleAsync"/> runs it through <see cref="RecurringJobs"/>.</summary>
    /// <param name="now">The moment this pass started.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    private async Task RunAsync(DateTimeOffset now, CancellationToken ct)
    {
        if (_options.Value.DisableReleaseUpdateChecks)
        {
            _logger.LogInformation("CheckReleaseUpdatesCron: release update checks are disabled; skipping.");
            return;
        }

        var release = await _github.GetLatestAsync(ct);
        if (release is null)
        {
            _logger.LogWarning("CheckReleaseUpdatesCron: GitHub returned no published release.");
            return;
        }

        var existing = await _context.LatestReleases
            .SingleOrDefaultAsync(r => r.Id == LatestRelease.SingletonId, ct);

        if (existing is null)
        {
            _context.LatestReleases.Add(
                LatestRelease.Create(release.Version, release.PublishedAt, release.ReleaseNotesUrl, now));
        }
        else
        {
            existing.Update(release.Version, release.PublishedAt, release.ReleaseNotesUrl, now);
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "CheckReleaseUpdatesCron: cached latest release {Version} (published {PublishedAt:u}).",
            release.Version, release.PublishedAt);
    }
}
