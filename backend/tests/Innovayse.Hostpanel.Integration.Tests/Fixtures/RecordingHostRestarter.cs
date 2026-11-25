using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The host restarter of the test host: it counts the restarts that were asked for and stops nothing, because a real restart
/// would stop the test host the other tests share.
/// </summary>
public sealed class RecordingHostRestarter : IHostRestarter
{
    /// <summary>How many restarts were asked for.</summary>
    private int _restarts;

    /// <summary>Gets how many restarts were asked for.</summary>
    public int Restarts => Volatile.Read(ref _restarts);

    /// <inheritdoc/>
    public bool IsRestarting => Restarts > 0;

    /// <inheritdoc/>
    public void Restart()
    {
        Interlocked.Increment(ref _restarts);
    }
}
