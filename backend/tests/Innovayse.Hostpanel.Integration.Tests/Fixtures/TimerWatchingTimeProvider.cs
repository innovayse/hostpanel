using Microsoft.Extensions.Time.Testing;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A <see cref="FakeTimeProvider"/> that tells a test when code under test has started waiting on it. A wait on a fake clock
/// ends only when the test moves the clock past it, and moving it before the wait began would be lost, so the test first
/// waits for the wait to exist: no sleeping, no guessing.
/// </summary>
public sealed class TimerWatchingTimeProvider : FakeTimeProvider
{
    /// <summary>Released once for every timer created, and taken once by every <see cref="NextTimerAsync"/>.</summary>
    private readonly SemaphoreSlim _created = new(0);

    /// <summary>Initializes a new instance of the <see cref="TimerWatchingTimeProvider"/> class.</summary>
    /// <param name="start">The instant the clock starts at.</param>
    public TimerWatchingTimeProvider(DateTimeOffset start)
        : base(start)
    {
    }

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);
        _created.Release();
        return timer;
    }

    /// <summary>Waits until one more timer has been created than this has already waited for.</summary>
    /// <param name="patience">How long to wait before the test calls the code under test stuck.</param>
    /// <returns>A task that completes when that timer exists.</returns>
    /// <exception cref="TimeoutException">No timer was created within <paramref name="patience"/>.</exception>
    public async Task NextTimerAsync(TimeSpan patience)
    {
        if (!await _created.WaitAsync(patience))
        {
            throw new TimeoutException($"The code under test created no timer on the test clock within {patience}.");
        }
    }
}
