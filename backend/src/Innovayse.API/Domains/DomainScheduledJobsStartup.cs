namespace Innovayse.API.Domains;

using Innovayse.Application.Domains.Commands.AutoRenewDomains;
using Innovayse.Application.Domains.Commands.CheckDomainExpiries;
using Wolverine;

/// <summary>
/// Hosted service that schedules the daily domain maintenance jobs on application startup.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CheckDomainExpiriesCommand"/> fires daily at 09:00 UTC.<br/>
/// <see cref="AutoRenewDomainsCommand"/> fires daily at 10:00 UTC.
/// </para>
/// <para>
/// Each handler is responsible for re-scheduling itself at the end of its run so that
/// the jobs continue on subsequent days without restarting the application.
/// </para>
/// </remarks>
public sealed class DomainScheduledJobsStartup(IServiceScopeFactory scopeFactory, ILogger<DomainScheduledJobsStartup> logger)
    : IHostedService
{
    /// <summary>Hour (UTC) at which <see cref="CheckDomainExpiriesCommand"/> should execute.</summary>
    private const int ExpiryCheckHour = 9;

    /// <summary>Hour (UTC) at which <see cref="AutoRenewDomainsCommand"/> should execute.</summary>
    private const int AutoRenewHour = 10;

    /// <summary>
    /// Schedules both domain jobs for their next daily occurrence.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token provided by the host.</param>
    /// <returns>A <see cref="Task"/> that completes once both messages are enqueued.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var expiryCheckTime = NextDailyOccurrence(ExpiryCheckHour);
        var autoRenewTime = NextDailyOccurrence(AutoRenewHour);

        await using var scope = scopeFactory.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await bus.ScheduleAsync(new CheckDomainExpiriesCommand(), expiryCheckTime);
        logger.LogInformation(
            "CheckDomainExpiriesCommand scheduled for {ScheduledAt:u}.",
            expiryCheckTime);

        await bus.ScheduleAsync(new AutoRenewDomainsCommand(), autoRenewTime);
        logger.LogInformation(
            "AutoRenewDomainsCommand scheduled for {ScheduledAt:u}.",
            autoRenewTime);
    }

    /// <summary>
    /// No-op — scheduled messages persist in the outbox; no cleanup is required on shutdown.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token provided by the host.</param>
    /// <returns>A completed <see cref="Task"/>.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Calculates the next UTC <see cref="DateTimeOffset"/> at which the given hour occurs.
    /// If the hour has already passed today, returns the same hour tomorrow.
    /// </summary>
    /// <param name="hour">Target hour in UTC (0–23).</param>
    /// <returns>The next occurrence of the specified hour in UTC.</returns>
    private static DateTimeOffset NextDailyOccurrence(int hour)
    {
        var now = DateTimeOffset.UtcNow;
        var candidate = new DateTimeOffset(now.Year, now.Month, now.Day, hour, 0, 0, TimeSpan.Zero);

        return candidate > now ? candidate : candidate.AddDays(1);
    }
}
