namespace Innovayse.Application.Provisioning.Commands.AutoTerminateSuspendedCron;

using Innovayse.Application.Common;
using Innovayse.Domain.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Wolverine;

/// <summary>
/// Handles <see cref="AutoTerminateSuspendedCronCommand"/> by terminating all services
/// that have been suspended longer than <see cref="SuspensionLimitDays"/>.
/// Re-schedules itself for the next day after completion.
/// </summary>
public sealed class AutoTerminateSuspendedCronHandler(
    IClientServiceRepository serviceRepo,
    IUnitOfWork uow,
    IMessageBus bus,
    ILogger<AutoTerminateSuspendedCronHandler> logger)
{
    /// <summary>Hour (UTC) at which this job runs daily.</summary>
    private const int CronHour = 8;

    /// <summary>Number of days a service can remain suspended before automatic termination.</summary>
    private const int SuspensionLimitDays = 14;

    /// <summary>
    /// Queries all services suspended beyond the allowed limit and terminates them.
    /// Each terminated service raises domain events for provisioning teardown and notifications.
    /// </summary>
    /// <param name="cmd">The scheduled command (marker, no payload).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(AutoTerminateSuspendedCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;

        var threshold = DateTimeOffset.UtcNow.AddDays(-SuspensionLimitDays);
        var services = await serviceRepo.ListSuspendedBeforeAsync(threshold, ct);

        foreach (var service in services)
        {
            service.Terminate($"Auto-terminated: suspended for over {SuspensionLimitDays} days");
        }

        if (services.Count > 0)
        {
            await uow.SaveChangesAsync(ct);
        }

        logger.LogInformation(
            "AutoTerminateSuspendedCron terminated {Count} services suspended over {Days} days.",
            services.Count,
            SuspensionLimitDays);

        // Re-schedule for next day
        var nextRun = NextDailyOccurrence(CronHour);
        await bus.ScheduleAsync(new AutoTerminateSuspendedCronCommand(), nextRun);
        logger.LogInformation("AutoTerminateSuspendedCronCommand re-scheduled for {NextRun:u}.", nextRun);
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
