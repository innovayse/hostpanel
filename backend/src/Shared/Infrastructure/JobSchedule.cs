namespace Innovayse.Hostpanel.Shared.Infrastructure;

/// <summary>
/// When a recurring job runs: once a day at a fixed UTC hour, or a fixed interval after its previous
/// run ended. The one place that turns a job's configured hour or interval into its next run and
/// into the question "did the last run miss its turn?".
/// </summary>
public sealed record JobSchedule
{
    /// <summary>The UTC hour of a daily job; <see langword="null"/> for an interval job.</summary>
    private readonly int? _hour;

    /// <summary>The gap between two runs of an interval job; <see cref="TimeSpan.Zero"/> for a daily job.</summary>
    private readonly TimeSpan _interval;

    /// <summary>Initializes a new instance of the <see cref="JobSchedule"/> class.</summary>
    /// <param name="hour">The UTC hour of a daily job, or <see langword="null"/>.</param>
    /// <param name="interval">The gap between runs of an interval job.</param>
    private JobSchedule(int? hour, TimeSpan interval)
    {
        _hour = hour;
        _interval = interval;
    }

    /// <summary>A job that runs once a day at a fixed hour.</summary>
    /// <param name="hour">The UTC hour (0-23).</param>
    /// <returns>The schedule.</returns>
    public static JobSchedule Daily(int hour)
    {
        return new JobSchedule(hour, TimeSpan.Zero);
    }

    /// <summary>A job that runs again a fixed time after its previous run ended.</summary>
    /// <param name="interval">The gap between the end of one run and the start of the next; positive.</param>
    /// <returns>The schedule.</returns>
    public static JobSchedule Every(TimeSpan interval)
    {
        return new JobSchedule(null, interval);
    }

    /// <summary>When the next run falls, counted from the moment a run ended or was skipped.</summary>
    /// <param name="now">The current moment.</param>
    /// <returns>The next daily occurrence of the hour, or <paramref name="now"/> plus the interval.</returns>
    public DateTimeOffset NextAfter(DateTimeOffset now)
    {
        return _hour is { } hour ? DailySchedule.NextOccurrence(hour, now) : now + _interval;
    }

    /// <summary>
    /// How long a job stays closed to other instances after a pass that succeeded. An interval job never
    /// stays closed for more than half its interval, or a short interval (the fulfilment dispatch runs
    /// every few seconds) would find its own job closed at every second beat and run at half its pace.
    /// </summary>
    /// <param name="configured">The rest configured for all jobs.</param>
    /// <returns><paramref name="configured"/>, or half the interval of an interval job when that is shorter.</returns>
    public TimeSpan RestAfterSuccess(TimeSpan configured)
    {
        return _hour is null && _interval / 2 < configured ? _interval / 2 : configured;
    }

    /// <summary>Whether the job has missed its turn: it has never succeeded, or not since its latest due moment.</summary>
    /// <param name="lastSucceededAt">When the job last succeeded, or <see langword="null"/> when it never has.</param>
    /// <param name="now">The current moment.</param>
    /// <returns>
    /// For a daily job, <see langword="true"/> when the last success is before the most recent
    /// occurrence of the hour (so a restart during the run hour still runs the day); for an interval
    /// job, when the last success is a full interval old or more.
    /// </returns>
    public bool IsOverdue(DateTimeOffset? lastSucceededAt, DateTimeOffset now)
    {
        if (lastSucceededAt is not { } last)
        {
            return true;
        }

        if (_hour is { } hour)
        {
            // NextOccurrence is today's hour while it is still ahead and tomorrow's otherwise, so a
            // day earlier is the latest occurrence that has already passed.
            return last < DailySchedule.NextOccurrence(hour, now).AddDays(-1);
        }

        return now - last >= _interval;
    }
}
