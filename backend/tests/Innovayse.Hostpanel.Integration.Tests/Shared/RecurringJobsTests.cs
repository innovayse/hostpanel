using Innovayse.Hostpanel.Application.Billing.Commands.CheckOverdueInvoicesCron;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// Pins <see cref="RecurringJobs"/> on a clock the test holds still: one instance runs a job, a pass
/// that fails or is cancelled is handled, the next run is scheduled from the end of the pass, a
/// missed day is caught up on startup, and a host that is stopping ends the chain cleanly. The
/// clock stands ten days ahead of the real one, so a run scheduled in the cases below is never
/// delivered while the test runs.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RecurringJobsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    /// <summary>The job these cases run: a marker command with a real handler that is never dispatched here.</summary>
    private const string JobName = nameof(CheckOverdueInvoicesCronCommand);

    /// <summary>Midnight UTC, ten days from now: the day every case happens on.</summary>
    private static readonly DateTimeOffset Midnight = new(DateTimeOffset.UtcNow.AddDays(10).UtcDateTime.Date, TimeSpan.Zero);

    /// <summary>The fixed moment of every case: half an hour after the 05:00 run hour.</summary>
    private static readonly DateTimeOffset Now = Midnight.AddHours(5).AddMinutes(30);

    /// <summary>The next regular run of the 05:00 daily job, seen from <see cref="Now"/>.</summary>
    private static readonly DateTimeOffset NextRegularRun = Midnight.AddDays(1).AddHours(5);

    /// <summary>The daily schedule every case runs the job on.</summary>
    private static readonly JobSchedule Daily = JobSchedule.Daily(5);

    /// <summary>The host whose clock the case holds still.</summary>
    private ClockedHost _host = null!;

    /// <summary>Forgets the job's record and builds the host on the fixed clock.</summary>
    /// <returns>A task.</returns>
    public async Task InitializeAsync()
    {
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().JobRuns.ExecuteDeleteAsync();
        }

        _host = new ClockedHost(factory, Now);
    }

    /// <summary>Disposes the host.</summary>
    /// <returns>A completed task.</returns>
    public Task DisposeAsync()
    {
        _host.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>A pass that succeeds is recorded, and the next run is scheduled from the moment it ended.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task SuccessfulPass_IsRecordedAndSchedulesTheNextRun()
    {
        using var scope = _host.Services.CreateScope();
        var jobs = JobsIn(scope, new StoppableLifetime());
        var ran = 0;

        var (next, session) = await TrackedAsync(() => jobs.RunAsync<CheckOverdueInvoicesCronCommand>(
            Daily, () => { ran++; return Task.CompletedTask; }, CancellationToken.None));

        ran.Should().Be(1);
        next.Should().Be(NextRegularRun);
        ScheduledRuns(session).Should().Be(1);
        (await scope.ServiceProvider.GetRequiredService<IJobLease>().GetLastSucceededAtAsync(JobName, CancellationToken.None))
            .Should().Be(Now);
    }

    /// <summary>A pass that throws is swallowed, is not recorded as a success, and the next run is still scheduled.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task FailingPass_IsNotRecordedAsASuccessButStillSchedulesTheNextRun()
    {
        using var scope = _host.Services.CreateScope();
        var jobs = JobsIn(scope, new StoppableLifetime());

        var (next, session) = await TrackedAsync(() => jobs.RunAsync<CheckOverdueInvoicesCronCommand>(
            Daily, () => throw new InvalidOperationException("database down"), CancellationToken.None));

        next.Should().Be(NextRegularRun);
        ScheduledRuns(session).Should().Be(1);
        (await scope.ServiceProvider.GetRequiredService<IJobLease>().GetLastSucceededAtAsync(JobName, CancellationToken.None))
            .Should().BeNull();
    }

    /// <summary>Another instance holds the job: this one skips the pass but keeps its own chain going.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task HeldJob_IsSkippedAndTheNextRunIsStillScheduled()
    {
        using var scope = _host.Services.CreateScope();
        var jobs = JobsIn(scope, new StoppableLifetime());
        await scope.ServiceProvider.GetRequiredService<IJobLease>().TryAcquireAsync(
            new JobLeaseClaim(JobName, "other-instance", Now, TimeSpan.FromHours(1)), CancellationToken.None);
        var ran = 0;

        var (next, session) = await TrackedAsync(() => jobs.RunAsync<CheckOverdueInvoicesCronCommand>(
            Daily, () => { ran++; return Task.CompletedTask; }, CancellationToken.None));

        ran.Should().Be(0);
        next.Should().Be(NextRegularRun);
        ScheduledRuns(session).Should().Be(1);
    }

    /// <summary>A pass started on demand runs under the lease and records its success, and schedules no next run.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task OnDemandPass_RunsUnderTheLeaseAndSchedulesNothing()
    {
        using var scope = _host.Services.CreateScope();
        var jobs = JobsIn(scope, new StoppableLifetime());
        var ran = 0;

        var (_, session) = await TrackedAsync(async () =>
        {
            await jobs.RunOnceAsync<CheckOverdueInvoicesCronCommand>(Daily, () => { ran++; return Task.CompletedTask; }, CancellationToken.None);
            return null;
        });

        ran.Should().Be(1);
        ScheduledRuns(session).Should().Be(0);
        (await scope.ServiceProvider.GetRequiredService<IJobLease>().GetLastSucceededAtAsync(JobName, CancellationToken.None))
            .Should().Be(Now);
    }

    /// <summary>A pass started on demand while another instance holds the job is skipped.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task OnDemandPass_OfAHeldJob_IsSkipped()
    {
        using var scope = _host.Services.CreateScope();
        var jobs = JobsIn(scope, new StoppableLifetime());
        await scope.ServiceProvider.GetRequiredService<IJobLease>().TryAcquireAsync(
            new JobLeaseClaim(JobName, "other-instance", Now, TimeSpan.FromHours(1)), CancellationToken.None);
        var ran = 0;

        await jobs.RunOnceAsync<CheckOverdueInvoicesCronCommand>(Daily, () => { ran++; return Task.CompletedTask; }, CancellationToken.None);

        ran.Should().Be(0);
    }

    /// <summary>A pass started on demand that throws is logged and swallowed, like a scheduled one.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task OnDemandPass_ThatThrows_IsSwallowed()
    {
        using var scope = _host.Services.CreateScope();
        var jobs = JobsIn(scope, new StoppableLifetime());

        var act = () => jobs.RunOnceAsync<CheckOverdueInvoicesCronCommand>(
            Daily, () => throw new InvalidOperationException("boom"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    /// <summary>A holder that crashed left its lease behind; once it has expired this instance runs the pass.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ExpiredLeaseOfACrashedHolder_IsTakenOverAndThePassRuns()
    {
        using var scope = _host.Services.CreateScope();
        var jobs = JobsIn(scope, new StoppableLifetime());
        await scope.ServiceProvider.GetRequiredService<IJobLease>().TryAcquireAsync(
            new JobLeaseClaim(JobName, "crashed", Now.AddHours(-2), TimeSpan.FromHours(1)), CancellationToken.None);
        var ran = 0;

        await TrackedAsync(() => jobs.RunAsync<CheckOverdueInvoicesCronCommand>(
            Daily, () => { ran++; return Task.CompletedTask; }, CancellationToken.None));

        ran.Should().Be(1);
    }

    /// <summary>
    /// A pass cut off because the host is stopping schedules nothing: the chain ends cleanly, no
    /// failure is logged for it, and the lease is given back so the next start can run the job.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task PassCancelledByAStoppingHost_SchedulesNothingAndFreesTheLease()
    {
        using var scope = _host.Services.CreateScope();
        var lifetime = new StoppableLifetime();
        var jobs = JobsIn(scope, lifetime);
        using var cts = new CancellationTokenSource();

        var (next, session) = await TrackedAsync(() => jobs.RunAsync<CheckOverdueInvoicesCronCommand>(
            Daily,
            async () =>
            {
                lifetime.StopApplication();
                await cts.CancelAsync();
                cts.Token.ThrowIfCancellationRequested();
            },
            cts.Token));

        next.Should().BeNull();
        ScheduledRuns(session).Should().Be(0);
        var lease = scope.ServiceProvider.GetRequiredService<IJobLease>();
        (await lease.TryAcquireAsync(new JobLeaseClaim(JobName, "next-start", Now, TimeSpan.FromHours(1)), CancellationToken.None))
            .Acquired.Should().BeTrue("the cancelled pass gave its lease back");
    }

    /// <summary>
    /// A pass cancelled while the host keeps running (Wolverine cancels a handler that outruns its
    /// execution timeout) still schedules the next run, or the chain would end with the host alive.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task PassCancelledByATimeout_StillSchedulesTheNextRun()
    {
        using var scope = _host.Services.CreateScope();
        var jobs = JobsIn(scope, new StoppableLifetime());
        using var cts = new CancellationTokenSource();

        var (next, session) = await TrackedAsync(() => jobs.RunAsync<CheckOverdueInvoicesCronCommand>(
            Daily,
            async () =>
            {
                await cts.CancelAsync();
                cts.Token.ThrowIfCancellationRequested();
            },
            cts.Token));

        next.Should().Be(NextRegularRun);
        ScheduledRuns(session).Should().Be(1);
    }

    /// <summary>The host was down through the run hour: startup runs the job now instead of waiting a day.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Startup_AfterAMissedDay_SchedulesTheCatchUpRunNow()
    {
        using var scope = _host.Services.CreateScope();
        var jobs = JobsIn(scope, new StoppableLifetime());
        await RecordSuccessAsync(scope, Now.AddDays(-2));

        var (first, session) = await TrackedAsync(() => jobs.StartAsync<CheckOverdueInvoicesCronCommand>(Daily, CancellationToken.None));

        first.Should().Be(Now);
        ScheduledRuns(session).Should().Be(1);
    }

    /// <summary>A restart after the day's run succeeded does not run it again; the first run is the next regular one.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Startup_AfterTodaysRunSucceeded_SchedulesTheNextRegularRun()
    {
        using var scope = _host.Services.CreateScope();
        var jobs = JobsIn(scope, new StoppableLifetime());
        await RecordSuccessAsync(scope, Now.AddMinutes(-20));

        var (first, session) = await TrackedAsync(() => jobs.StartAsync<CheckOverdueInvoicesCronCommand>(Daily, CancellationToken.None));

        first.Should().Be(NextRegularRun);
        ScheduledRuns(session).Should().Be(1);
    }

    /// <summary>A job that has never run is caught up at once on its first startup.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Startup_OfAJobThatNeverRan_SchedulesItNow()
    {
        using var scope = _host.Services.CreateScope();
        var jobs = JobsIn(scope, new StoppableLifetime());

        var (first, _) = await TrackedAsync(() => jobs.StartAsync<CheckOverdueInvoicesCronCommand>(Daily, CancellationToken.None));

        first.Should().Be(Now);
    }

    /// <summary>
    /// Builds the job runner the way the container does, except for the host lifetime, which a case
    /// stops by hand, and the retry delay, which is zero so a failing case does not wait.
    /// </summary>
    /// <param name="scope">The scope of the clocked host the runner reads its lease, bus and clock from.</param>
    /// <param name="lifetime">The lifetime the case controls.</param>
    /// <returns>The runner.</returns>
    private static RecurringJobs JobsIn(IServiceScope scope, IHostApplicationLifetime lifetime)
    {
        var services = scope.ServiceProvider;
        return new RecurringJobs(
            services.GetRequiredService<IJobLease>(),
            services.GetRequiredService<IMessageBus>(),
            services.GetRequiredService<IClock>(),
            Options.Create(new ScheduledJobsOptions { RestSeconds = 0, ScheduleRetryDelaySeconds = 0 }),
            lifetime,
            NullLogger<RecurringJobs>.Instance);
    }

    /// <summary>Records a successful pass of the job at a given moment.</summary>
    /// <param name="scope">The scope to take the lease from.</param>
    /// <param name="at">When the pass succeeded.</param>
    /// <returns>A task.</returns>
    private static async Task RecordSuccessAsync(IServiceScope scope, DateTimeOffset at)
    {
        var lease = scope.ServiceProvider.GetRequiredService<IJobLease>();
        await lease.TryAcquireAsync(new JobLeaseClaim(JobName, "earlier-run", at, TimeSpan.FromMinutes(5)), CancellationToken.None);
        await lease.CompleteAsync(new JobLeaseRelease(JobName, "earlier-run", at, true, TimeSpan.Zero), CancellationToken.None);
    }

    /// <summary>Runs an action on the clocked host while Wolverine records what it sends.</summary>
    /// <param name="action">What to run; its result is returned.</param>
    /// <returns>The action's result and the tracked session.</returns>
    private async Task<(DateTimeOffset? Result, ITrackedSession Session)> TrackedAsync(Func<Task<DateTimeOffset?>> action)
    {
        DateTimeOffset? result = null;
        var session = await _host.Services.GetRequiredService<IHost>().TrackActivity()
            .DoNotAssertOnExceptionsDetected()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync(async Task (IMessageContext _) => result = await action());

        return (result, session);
    }

    /// <summary>
    /// How many runs of the job were scheduled during a tracked session. Wolverine records a message
    /// sent to a local queue for later as sent with the scheduled status; the record does not keep the
    /// moment, which the runner returns and each case checks on its own.
    /// </summary>
    /// <param name="session">The tracked session.</param>
    /// <returns>The number of scheduled runs.</returns>
    private static int ScheduledRuns(ITrackedSession session)
    {
        return session.Sent.Envelopes()
            .Count(e => e.Message is CheckOverdueInvoicesCronCommand && e.Status == EnvelopeStatus.Scheduled);
    }
}
