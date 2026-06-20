namespace Innovayse.Application.Servers.Commands.SyncServerAccountCountsCron;

using Innovayse.Application.Admin.Servers.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Servers.Interfaces;
using Microsoft.Extensions.Logging;
using Wolverine;

/// <summary>
/// Handles <see cref="SyncServerAccountCountsCronCommand"/> by testing connectivity to each
/// active server and recording its current account count. Re-schedules itself daily.
/// </summary>
public sealed class SyncServerAccountCountsCronHandler(
    IServerRepository serverRepo,
    IServerConnectionTester connectionTester,
    IUnitOfWork uow,
    IMessageBus bus,
    ILogger<SyncServerAccountCountsCronHandler> logger)
{
    /// <summary>Hour (UTC) at which this job runs daily.</summary>
    private const int CronHour = 6;

    /// <summary>
    /// Iterates all non-disabled servers, tests connectivity to each, and records
    /// the connection status and account count. Failures on individual servers
    /// are caught and logged so remaining servers still get synced.
    /// </summary>
    /// <param name="cmd">The scheduled command (marker, no payload).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(SyncServerAccountCountsCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;

        var servers = await serverRepo.ListAsync(null, ct);
        var syncedCount = 0;

        foreach (var server in servers)
        {
            if (server.IsDisabled)
            {
                logger.LogDebug(
                    "Skipping disabled server {ServerId} ({ServerName}).", server.Id, server.Name);
                continue;
            }

            try
            {
                var (connected, accountsCount, _, errorMessage) =
                    await connectionTester.TestAsync(server, ct);

                server.RecordConnectionTest(connected, accountsCount);
                await uow.SaveChangesAsync(ct);
                syncedCount++;

                if (!connected)
                {
                    logger.LogWarning(
                        "Server {ServerId} ({ServerName}) connection test failed: {Error}.",
                        server.Id, server.Name, errorMessage);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Unexpected error syncing account count for server {ServerId} ({ServerName}).",
                    server.Id, server.Name);
            }
        }

        logger.LogInformation(
            "SyncServerAccountCountsCron synced {SyncedCount} of {TotalCount} servers.",
            syncedCount, servers.Count);

        // Re-schedule for next day
        var nextRun = NextDailyOccurrence(CronHour);
        await bus.ScheduleAsync(new SyncServerAccountCountsCronCommand(), nextRun);
        logger.LogInformation("SyncServerAccountCountsCronCommand re-scheduled for {NextRun:u}.", nextRun);
    }

    /// <summary>
    /// Calculates the next UTC occurrence of the given hour.
    /// If the hour has already passed today, returns the same hour tomorrow.
    /// </summary>
    /// <param name="hour">Target hour in UTC (0-23).</param>
    /// <returns>The next occurrence of the specified hour in UTC.</returns>
    private static DateTimeOffset NextDailyOccurrence(int hour)
    {
        var now = DateTimeOffset.UtcNow;
        var candidate = new DateTimeOffset(now.Year, now.Month, now.Day, hour, 0, 0, TimeSpan.Zero);
        return candidate > now ? candidate : candidate.AddDays(1);
    }
}
