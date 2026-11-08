using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.EnrollRegistryInstanceCron;

/// <summary>
/// Scheduled handler that brings this installation's registry identity to Verified. A recurring background message, not
/// dispatched by a caller, so it returns <see cref="Task"/>; the outcome of a pass is saved on the identity and shown on the
/// plugins page, and the enrolment logs a failure by its code.
/// </summary>
/// <remarks>
/// The pass is cheap while there is nothing to do: a verified identity, a switched-off registry, an unusable address and a
/// back-off that has not run out all answer without a call to the registry, so the job runs every minute and the back-off
/// stored on the identity, not the job's interval, decides how often the registry is asked.
/// </remarks>
public sealed class EnrollRegistryInstanceCronHandler
{
    /// <summary>Registers and verifies the installation.</summary>
    private readonly RegistryInstanceEnrollment _enrollment;

    /// <summary>Runs the pass under its lease and schedules the next one.</summary>
    private readonly RecurringJobs _jobs;

    /// <summary>Initializes the handler.</summary>
    /// <param name="enrollment">Registers and verifies the installation.</param>
    /// <param name="jobs">Runs the pass under its lease and schedules the next one.</param>
    public EnrollRegistryInstanceCronHandler(RegistryInstanceEnrollment enrollment, RecurringJobs jobs)
    {
        _enrollment = enrollment;
        _jobs = jobs;
    }

    /// <summary>Handles the command: runs one pass through <see cref="RecurringJobs"/>, which schedules the next.</summary>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the pass has run and the next one is scheduled.</returns>
    public Task HandleAsync(EnrollRegistryInstanceCronCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        return _jobs.RunAsync<EnrollRegistryInstanceCronCommand>(
            _jobs.Schedules.EnrollRegistryInstance, () => RunAsync(ct), ct);
    }

    /// <summary>One pass: enrols without forcing, so the back-off and a revocation are respected.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the attempt, if one was due, is saved.</returns>
    private async Task RunAsync(CancellationToken ct)
    {
        // The outcome is on the identity and in the log already; a failed attempt is not a failed pass.
        await _enrollment.EnsureVerifiedAsync(force: false, ct);
    }
}
