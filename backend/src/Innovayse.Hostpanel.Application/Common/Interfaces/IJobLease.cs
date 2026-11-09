namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The shared record of who runs each recurring job. With several instances of the application
/// running, an instance takes a job's lease before a pass, so only one runs it, and gives it back
/// after. A holder that dies never gives it back, and the lease expires on its own.
/// </summary>
public interface IJobLease
{
    /// <summary>Takes a job's lease when nobody holds it.</summary>
    /// <param name="claim">Which job, who asks, the moment and how long the lease lasts.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the caller now holds the lease, and who holds it when it does not.</returns>
    public Task<JobLeaseDecision> TryAcquireAsync(JobLeaseClaim claim, CancellationToken ct);

    /// <summary>Gives a job's lease back after a pass and records how the pass ended.</summary>
    /// <param name="release">Which job, who held it, how the pass ended and the moment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    public Task CompleteAsync(JobLeaseRelease release, CancellationToken ct);

    /// <summary>Reads when a job last succeeded.</summary>
    /// <param name="jobName">The job's name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The moment of the last successful pass, or <see langword="null"/> when the job has never succeeded.</returns>
    public Task<DateTimeOffset?> GetLastSucceededAtAsync(string jobName, CancellationToken ct);
}
