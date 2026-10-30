using Innovayse.Hostpanel.Application.Admin.Servers.Commands.SyncServerAccountCountsCron;
using Innovayse.Hostpanel.Application.Billing.Commands.CheckOverdueInvoicesCron;
using Innovayse.Hostpanel.Application.Billing.Commands.DispatchFulfilmentCron;
using Innovayse.Hostpanel.Application.Billing.Commands.ProcessBillableItemsCron;
using Innovayse.Hostpanel.Application.Billing.Commands.ProcessRenewalsCron;
using Innovayse.Hostpanel.Application.Billing.Commands.ReconcileGatewayPaymentsCron;
using Innovayse.Hostpanel.Application.Billing.Commands.SendRenewalRemindersCron;
using Innovayse.Hostpanel.Application.Billing.Commands.SuspendOverdueServicesCron;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.CheckDomainExpiries;
using Innovayse.Hostpanel.Application.Domains.Commands.SyncPendingDomainStatuses;
using Innovayse.Hostpanel.Application.Domains.Commands.SyncTldCostPrices;
using Innovayse.Hostpanel.Application.Domains.Commands.SyncTldCostPricesNow;
using Innovayse.Hostpanel.Application.Provisioning.Commands.AutoTerminateSuspendedCron;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// Every recurring job schedules its own next run. A run that throws (here: the database is
/// unreachable) must still schedule the next one, exactly once, or the chain stops silently until
/// the next restart. A job that never schedules itself runs once per restart and never again.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ScheduledJobsSurviveFailureTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Every self-scheduling job, as the command that runs it.</summary>
    public static TheoryData<object> Jobs => new()
    {
        new CheckOverdueInvoicesCronCommand(),
        new AutoTerminateSuspendedCronCommand(),
        new ProcessBillableItemsCronCommand(),
        new SyncServerAccountCountsCronCommand(),
        new SyncPendingDomainStatusesCommand(),
        new SyncTldCostPricesCommand(),
        new CheckDomainExpiriesCommand(),
        new ProcessRenewalsCronCommand(),
        new SuspendOverdueServicesCronCommand(),
        new SendRenewalRemindersCronCommand(),
        new ReconcileGatewayPaymentsCronCommand(),
        new DispatchFulfilmentCronCommand(),
    };

    /// <summary>
    /// An admin's "sync now" of TLD cost prices runs once and schedules nothing: the daily chain
    /// already has its next run, and scheduling here started one more chain per click.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ManualTldSync_SchedulesNoNextRun()
    {
        var host = factory.Services.GetRequiredService<IHost>();

        var session = await host.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new SyncTldCostPricesNowCommand());

        NextRunsOf(session, typeof(SyncTldCostPricesCommand)).Should().Be(0);
    }

    /// <summary>
    /// Counts the runs of a job sent during the session other than the one being handled. A run a
    /// handler schedules on a local queue is recorded as a send of a new envelope; the record a
    /// failed handler leaves carries the handled envelope itself, so it is not counted.
    /// </summary>
    /// <param name="session">The tracked session.</param>
    /// <param name="jobType">The job's command type.</param>
    /// <returns>How many next runs were sent.</returns>
    private static int NextRunsOf(ITrackedSession session, Type jobType)
    {
        // None when the session handled a different message (an admin's sync-now, say).
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

    /// <summary>
    /// A host whose data context points at a database nobody listens on, so the first query of
    /// any job throws, as it does when the real database is down; log lines are recorded.
    /// </summary>
    /// <param name="logs">Receives every log line the host writes.</param>
    /// <returns>The host.</returns>
    private WebApplicationFactory<Program> HostWithUnreachableDatabase(RecordingLoggerProvider logs) =>
        factory.WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(s =>
            {
                s.AddSingleton<ILoggerProvider>(logs);
                s.AddScoped<IHostpanelDbContext>(_ => new AppDbContext(
                    new DbContextOptionsBuilder<AppDbContext>()
                        .UseNpgsql("Host=127.0.0.1;Port=1;Database=unreachable;Username=x;Password=x;Timeout=1")
                        .Options,
                    new ModelOnlyEncryptionService()));
            });
        });

    /// <summary>
    /// A job whose run throws still schedules its next run, and only one, and logs the failure.
    /// Sent through the local queue as a scheduled run is, so any retry Wolverine made would show
    /// up as a second next run.
    /// </summary>
    /// <param name="job">The job's command.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(Jobs))]
    public async Task FailedRun_StillSchedulesTheNextRunOnce(object job)
    {
        var logs = new RecordingLoggerProvider();
        var host = HostWithUnreachableDatabase(logs).Services.GetRequiredService<IHost>();

        var session = await host.TrackActivity()
            .DoNotAssertOnExceptionsDetected()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync(c => c.SendAsync(job).AsTask());

        logs.Entries.Should().Contain(
            e => e.Level == LogLevel.Error && e.Message.Contains(job.GetType().Name) && e.Message.Contains("failed"),
            "the unreachable database must make the run fail, and the failure must be logged");
        NextRunsOf(session, job.GetType())
            .Should().Be(1, "the next run is scheduled once, even though this run failed");
    }

    /// <summary>The domain expiry check schedules its next run after a run that succeeds.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task DomainExpiryCheck_SchedulesTheNextRun()
    {
        var host = factory.Services.GetRequiredService<IHost>();

        var session = await host.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new CheckDomainExpiriesCommand());

        NextRunsOf(session, typeof(CheckDomainExpiriesCommand)).Should().Be(1);
    }
}
