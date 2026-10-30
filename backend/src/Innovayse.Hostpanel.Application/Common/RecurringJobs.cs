using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Common;

/// <summary>
/// Runs the passes of the recurring jobs and keeps their chains alive. A job is a marker command
/// that its handler runs and then schedules again; every handler (and every startup service) goes
/// through this class, so the rules below are written once.
/// </summary>
/// <remarks>
/// <para>
/// One instance runs a job at a time: a pass takes the job's lease first (<see cref="IJobLease"/>)
/// and gives it back after, so with several instances of the application a job is not run twice.
/// An instance that does not get the lease skips the pass and still schedules its own next one, so
/// the chain survives the instance that holds the job going away.
/// </para>
/// <para>
/// The next run is scheduled from the moment the pass ended, so a long pass never overlaps the
/// next one. A failing pass is caught and logged and the next run is still scheduled; a handler
/// that throws would have the messages it holds discarded by Wolverine, the next run included.
/// Scheduling the next run is retried, and a failure that outlasts the retries is logged.
/// </para>
/// <para>
/// The one pass that ends the chain on purpose is one cut off because the host is stopping: its
/// scheduled messages would be lost with the process anyway, and the next start catches the job up
/// from <see cref="IJobLease.GetLastSucceededAtAsync"/>. A pass cancelled any other way (Wolverine
/// cancels a handler that outruns its execution timeout) is logged and the chain continues.
/// </para>
/// </remarks>
public sealed class RecurringJobs
{
    /// <summary>Takes and gives back each job's lease and remembers when it last succeeded.</summary>
    private readonly IJobLease _lease;

    /// <summary>Message bus the next runs are scheduled on.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock every moment here is read from.</summary>
    private readonly IClock _clock;

    /// <summary>The hours, intervals and lease timings of the jobs.</summary>
    private readonly IOptions<ScheduledJobsOptions> _options;

    /// <summary>Tells whether the host is stopping, as opposed to a pass being cancelled for running too long.</summary>
    private readonly IHostApplicationLifetime _lifetime;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<RecurringJobs> _logger;

    /// <summary>Initializes a new instance of the <see cref="RecurringJobs"/> class.</summary>
    /// <param name="lease">Takes and gives back each job's lease and remembers when it last succeeded.</param>
    /// <param name="bus">Message bus the next runs are scheduled on.</param>
    /// <param name="clock">The clock every moment here is read from.</param>
    /// <param name="options">The hours, intervals and lease timings of the jobs.</param>
    /// <param name="lifetime">Tells whether the host is stopping.</param>
    /// <param name="logger">Structured logger.</param>
    public RecurringJobs(
        IJobLease lease,
        IMessageBus bus,
        IClock clock,
        IOptions<ScheduledJobsOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<RecurringJobs> logger)
    {
        _lease = lease;
        _bus = bus;
        _clock = clock;
        _options = options;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <summary>Gets the configured hours and intervals of the jobs, for a handler to name its own schedule.</summary>
    public ScheduledJobsOptions Schedules => _options.Value;

    /// <summary>
    /// Schedules a job's first run of this process: right now when it missed its turn (it has never
    /// succeeded, or not since its latest due moment, say because the application was down or
    /// restarting during the run hour), at its next regular moment otherwise.
    /// </summary>
    /// <typeparam name="TCommand">The job's marker command.</typeparam>
    /// <param name="schedule">When the job runs.</param>
    /// <param name="ct">Cancellation token provided by the host.</param>
    /// <returns>The moment the first run was scheduled for, or <see langword="null"/> when scheduling failed (logged).</returns>
    public async Task<DateTimeOffset?> StartAsync<TCommand>(JobSchedule schedule, CancellationToken ct)
        where TCommand : IRecurringJob, new()
    {
        var jobName = typeof(TCommand).Name;
        var now = _clock.UtcNow;
        var overdue = false;

        try
        {
            overdue = schedule.IsOverdue(await _lease.GetLastSucceededAtAsync(jobName, ct), now);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Without the record the job cannot be caught up, but its regular chain must still start.
            _logger.LogError(ex, "{JobName} could not read when it last succeeded; it is scheduled for its next regular run.", jobName);
        }

        var first = overdue ? now : schedule.NextAfter(now);
        if (overdue)
        {
            _logger.LogInformation("{JobName} missed its last run and is caught up now.", jobName);
        }

        return await ScheduleAsync<TCommand>(first);
    }

    /// <summary>
    /// Runs one pass of a job under its lease, records how it ended, and schedules the next run.
    /// Never throws for a failed pass.
    /// </summary>
    /// <typeparam name="TCommand">The job's marker command; also names the job.</typeparam>
    /// <param name="schedule">When the job runs, for the next run.</param>
    /// <param name="run">The pass itself.</param>
    /// <param name="ct">The handler's cancellation token.</param>
    /// <returns>The moment the next run was scheduled for, or <see langword="null"/> when the chain ended (the host is stopping, or scheduling failed and was logged).</returns>
    public async Task<DateTimeOffset?> RunAsync<TCommand>(JobSchedule schedule, Func<Task> run, CancellationToken ct)
        where TCommand : IRecurringJob, new()
    {
        if (!await TryRunAsync<TCommand>(schedule, run, ct))
        {
            return null;
        }

        return await ScheduleAsync<TCommand>(schedule.NextAfter(_clock.UtcNow));
    }

    /// <summary>
    /// Runs one pass of a job under its lease exactly as <see cref="RunAsync{TCommand}"/> does, and
    /// schedules nothing: for a pass started on demand beside the scheduled chain (the fulfilment
    /// dispatch run right after a payment), where scheduling would start a second chain. The pass takes
    /// the same lease as the chain's, so the two never run at once. Never throws for a failed pass.
    /// </summary>
    /// <typeparam name="TCommand">The marker command of the job whose pass this is; also names the job and its lease.</typeparam>
    /// <param name="schedule">When the job runs; names how long the lease stays closed after a success.</param>
    /// <param name="run">The pass itself.</param>
    /// <param name="ct">The handler's cancellation token.</param>
    /// <returns>A task that completes when the pass ended or was skipped.</returns>
    public async Task RunOnceAsync<TCommand>(JobSchedule schedule, Func<Task> run, CancellationToken ct)
        where TCommand : IRecurringJob, new()
    {
        await TryRunAsync<TCommand>(schedule, run, ct);
    }

    /// <summary>Runs one pass under the lease and handles how it ended: a failure or a timeout is logged and swallowed.</summary>
    /// <typeparam name="TCommand">The job's marker command; also names the job.</typeparam>
    /// <param name="schedule">When the job runs.</param>
    /// <param name="run">The pass itself.</param>
    /// <param name="ct">The handler's cancellation token.</param>
    /// <returns><see langword="false"/> when the pass was cut off because the host is stopping, which ends a chain; <see langword="true"/> otherwise.</returns>
    private async Task<bool> TryRunAsync<TCommand>(JobSchedule schedule, Func<Task> run, CancellationToken ct)
        where TCommand : IRecurringJob, new()
    {
        var jobName = typeof(TCommand).Name;

        try
        {
            await RunUnderLeaseAsync(jobName, schedule, run, ct);
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            if (_lifetime.ApplicationStopping.IsCancellationRequested)
            {
                _logger.LogInformation("{JobName} was cut off because the host is stopping; the next start catches it up.", jobName);
                return false;
            }

            _logger.LogWarning(ex, "{JobName} was cancelled before it finished; its next run is still scheduled.", jobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{JobName} failed; its next run is still scheduled.", jobName);
        }

        return true;
    }

    /// <summary>Takes the lease, runs the pass when it was granted, and gives the lease back however the pass ended.</summary>
    /// <param name="jobName">The job's name.</param>
    /// <param name="schedule">When the job runs; names how long the lease stays closed after a success.</param>
    /// <param name="run">The pass itself.</param>
    /// <param name="ct">The handler's cancellation token.</param>
    /// <returns>A task; it faults with whatever the lease or the pass threw.</returns>
    private async Task RunUnderLeaseAsync(string jobName, JobSchedule schedule, Func<Task> run, CancellationToken ct)
    {
        var options = _options.Value;
        var owner = $"{Environment.MachineName}:{Guid.NewGuid():N}";

        var decision = await _lease.TryAcquireAsync(new JobLeaseClaim(jobName, owner, _clock.UtcNow, options.Lease), ct);
        if (!decision.Acquired)
        {
            _logger.LogInformation("{JobName} is held by {HeldBy}; this instance skips the pass.", jobName, decision.HeldBy);
            return;
        }

        var succeeded = false;
        try
        {
            await run();
            succeeded = true;
        }
        finally
        {
            var rest = schedule.RestAfterSuccess(options.Rest);
            await ReleaseAsync(new JobLeaseRelease(jobName, owner, _clock.UtcNow, succeeded, rest));
        }
    }

    /// <summary>Gives a lease back; a failure is logged, not thrown, so it never hides how the pass itself ended.</summary>
    /// <param name="release">What to report.</param>
    /// <returns>A task.</returns>
    private async Task ReleaseAsync(JobLeaseRelease release)
    {
        try
        {
            // Not cancellable: a pass cut off by shutdown still has to free its job.
            await _lease.CompleteAsync(release, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{JobName} could not give its lease back; the lease expires on its own.", release.JobName);
        }
    }

    /// <summary>Schedules a job's run, retrying when the bus refuses, and logs a failure that outlasts the retries.</summary>
    /// <typeparam name="TCommand">The job's marker command.</typeparam>
    /// <param name="at">When the run is due.</param>
    /// <returns><paramref name="at"/>, or <see langword="null"/> when every attempt failed or the host stopped meanwhile.</returns>
    private async Task<DateTimeOffset?> ScheduleAsync<TCommand>(DateTimeOffset at)
        where TCommand : IRecurringJob, new()
    {
        var jobName = typeof(TCommand).Name;
        var options = _options.Value;

        for (var attempt = 1; attempt <= options.ScheduleAttempts; attempt++)
        {
            try
            {
                await _bus.ScheduleAsync(new TCommand(), at);
                _logger.LogInformation("{JobName} scheduled for {ScheduledAt:u}.", jobName, at);
                return at;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex, "{JobName} could not be scheduled (attempt {Attempt} of {Attempts}).", jobName, attempt, options.ScheduleAttempts);
            }

            if (attempt < options.ScheduleAttempts && !await PauseAsync(options.ScheduleRetryDelay))
            {
                return null;
            }
        }

        _logger.LogError(
            "{JobName} could not be scheduled after {Attempts} attempts; the chain is broken until the next start catches it up.",
            jobName,
            options.ScheduleAttempts);
        return null;
    }

    /// <summary>Waits between two scheduling attempts, ending early when the host stops.</summary>
    /// <param name="delay">How long to wait.</param>
    /// <returns><see langword="true"/> when the wait ran its course; <see langword="false"/> when the host is stopping.</returns>
    private async Task<bool> PauseAsync(TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, _lifetime.ApplicationStopping);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
