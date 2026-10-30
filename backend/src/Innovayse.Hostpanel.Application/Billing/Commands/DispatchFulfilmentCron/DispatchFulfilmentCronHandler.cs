using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common;

namespace Innovayse.Hostpanel.Application.Billing.Commands.DispatchFulfilmentCron;

/// <summary>
/// Handles <see cref="DispatchFulfilmentCronCommand"/>: runs one pass of the fulfilment dispatch under the
/// job lease, then schedules itself from the end of the pass. The pass is bounded by the batch size and by
/// the time each plugin has to answer, and its chain has the job-lease timeout of every recurring job, so
/// the handler timeout never cuts a delivery short.
/// </summary>
public sealed class DispatchFulfilmentCronHandler
{
    /// <summary>Delivers the due records.</summary>
    private readonly FulfilmentDispatcher _dispatcher;

    /// <summary>Runs the passes under their lease and schedules the next run.</summary>
    private readonly RecurringJobs _jobs;

    /// <summary>Initializes the handler.</summary>
    /// <param name="dispatcher">Delivers the due records.</param>
    /// <param name="jobs">Runs the passes under their lease and schedules the next run.</param>
    public DispatchFulfilmentCronHandler(FulfilmentDispatcher dispatcher, RecurringJobs jobs)
    {
        _dispatcher = dispatcher;
        _jobs = jobs;
    }

    /// <summary>Handles the cron command.</summary>
    /// <param name="cmd">The cron command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the pass ended and the next run is scheduled.</returns>
    public Task HandleAsync(DispatchFulfilmentCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;

        return _jobs.RunAsync<DispatchFulfilmentCronCommand>(
            _jobs.Schedules.DispatchFulfilment, () => _dispatcher.RunAsync(ct), ct);
    }
}
