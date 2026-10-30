using Innovayse.Hostpanel.Application.Billing.Commands.DispatchFulfilmentCron;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common;

namespace Innovayse.Hostpanel.Application.Billing.Commands.DispatchFulfilmentNow;

/// <summary>
/// Handles <see cref="DispatchFulfilmentNowCommand"/>: one pass of the fulfilment dispatch under the same
/// job lease as the scheduled chain, so the two never deliver at once, and nothing scheduled.
/// </summary>
public sealed class DispatchFulfilmentNowHandler
{
    /// <summary>Delivers the due records.</summary>
    private readonly FulfilmentDispatcher _dispatcher;

    /// <summary>Runs the pass under the job lease.</summary>
    private readonly RecurringJobs _jobs;

    /// <summary>Initializes the handler.</summary>
    /// <param name="dispatcher">Delivers the due records.</param>
    /// <param name="jobs">Runs the pass under the job lease.</param>
    public DispatchFulfilmentNowHandler(FulfilmentDispatcher dispatcher, RecurringJobs jobs)
    {
        _dispatcher = dispatcher;
        _jobs = jobs;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The command (marker, no payload).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the pass ended or was skipped because another instance holds the job.</returns>
    public Task HandleAsync(DispatchFulfilmentNowCommand cmd, CancellationToken ct)
    {
        _ = cmd;

        // Named by the scheduled chain's command: one job, one lease, whichever message starts the pass.
        return _jobs.RunOnceAsync<DispatchFulfilmentCronCommand>(
            _jobs.Schedules.DispatchFulfilment, () => _dispatcher.RunAsync(ct), ct);
    }
}
