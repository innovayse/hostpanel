namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A meeting point for concurrent requests: each caller of <see cref="ArriveAsync"/> waits until
/// every party has arrived, then all continue together. A test puts one in the fake card gateway's
/// charge so that two pay requests have both read their invoice unpaid before either charge answers.
/// </summary>
public sealed class ChargeRendezvous
{
    /// <summary>The longest a party waits for the others before the test fails instead of hanging.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    /// <summary>How many parties must arrive before any continues.</summary>
    private readonly int _parties;

    /// <summary>Completes when the last party has arrived.</summary>
    private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>How many parties have arrived so far.</summary>
    private int _arrived;

    /// <summary>Initializes the rendezvous.</summary>
    /// <param name="parties">How many parties must arrive before any continues.</param>
    public ChargeRendezvous(int parties)
    {
        _parties = parties;
    }

    /// <summary>Waits until every party has arrived.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once all parties have arrived.</returns>
    /// <exception cref="TimeoutException">When the other parties do not arrive within the patience.</exception>
    public async Task ArriveAsync(CancellationToken ct)
    {
        if (Interlocked.Increment(ref _arrived) >= _parties)
        {
            _allArrived.TrySetResult();
        }

        await _allArrived.Task.WaitAsync(Patience, ct);
    }
}
