using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Commands.SyncServerAccountCountsCron;

/// <summary>
/// Handles <see cref="SyncServerAccountCountsCronCommand"/> by testing connectivity to each
/// active server and recording its current account count. Re-schedules itself daily.
/// </summary>
public sealed class SyncServerAccountCountsCronHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Tests connectivity and reads the account count off a server.</summary>
    private readonly IServerConnectionTester _connectionTester;

    /// <summary>Runs the passes under their lease and schedules the next run.</summary>
    private readonly RecurringJobs _jobs;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<SyncServerAccountCountsCronHandler> _logger;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="connectionTester">Tests connectivity and reads the account count off a server.</param>
    /// <param name="jobs">Runs the passes under their lease and schedules the next run.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The source of the current moment.</param>
    public SyncServerAccountCountsCronHandler(
        IHostpanelDbContext context,
        IServerConnectionTester connectionTester,
        RecurringJobs jobs,
        ILogger<SyncServerAccountCountsCronHandler> logger,
        IClock clock)
    {
        _context = context;
        _connectionTester = connectionTester;
        _jobs = jobs;
        _logger = logger;
        _clock = clock;
    }

    /// <summary>
    /// Iterates all non-disabled servers, tests connectivity to each, and records
    /// the connection status and account count. Failures on individual servers
    /// are caught and logged so remaining servers still get synced.
    /// </summary>
    /// <param name="cmd">The scheduled command (marker, no payload).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the message has been handled.</returns>
    public Task HandleAsync(SyncServerAccountCountsCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;

        return _jobs.RunAsync<SyncServerAccountCountsCronCommand>(
            _jobs.Schedules.SyncServerAccountCounts, () => RunAsync(ct), ct);
    }

    /// <summary>One pass of the job; <see cref="HandleAsync"/> runs it through <see cref="RecurringJobs"/>.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    private async Task RunAsync(CancellationToken ct)
    {
        var syncedCount = 0;
        var totalCount = 0;
        var lastId = 0;
        while (true)
        {
            // Tracked, not AsNoTracking: each server is mutated and saved below. Keyset batches, so a
            // long list is never loaded at once and a server that fails is not read again this pass.
            var servers = await _context.Servers
                .Where(s => s.Id > lastId && !s.IsDisabled)
                .OrderBy(s => s.Id)
                .Take(JobBatch.Size)
                .ToListAsync(ct);
            if (servers.Count == 0)
            {
                break;
            }

            foreach (var server in servers)
            {
                try
                {
                    var test = await _connectionTester.TestAsync(server, ct);

                    server.RecordConnectionTest(test.Connected, test.AccountsCount, _clock.UtcNow);
                    await _context.SaveChangesAsync(ct);
                    syncedCount++;

                    if (!test.Connected)
                    {
                        _logger.LogWarning(
                            "Server {ServerId} connection test failed: {ErrorCode}.", server.Id, test.ErrorCode);
                    }
                }
                catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
                {
                    // The failed change is still tracked and would ride along with, and fail, every later
                    // save in this loop; detaching it isolates this server, and the next run retries it.
                    _context.Servers.Entry(server).State = EntityState.Detached;
                    _logger.LogError(ex, "Unexpected error syncing account count for server {ServerId}.", server.Id);
                }
            }

            totalCount += servers.Count;
            lastId = servers[^1].Id;
            _context.DetachAll();
        }

        _logger.LogInformation(
            "SyncServerAccountCountsCron synced {SyncedCount} of {TotalCount} servers.",
            syncedCount, totalCount);
    }
}
