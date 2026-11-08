namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// How long registration waits after a failed attempt: a short pause that doubles with every failure in a row and stops
/// growing at one hour, so a registry that is down is asked a few times an hour and not every minute, and one that comes
/// back is found again within the hour.
/// </summary>
public static class RegistryInstanceBackoff
{
    /// <summary>The pause after the first failure.</summary>
    public static readonly TimeSpan First = TimeSpan.FromSeconds(30);

    /// <summary>The longest pause, however many attempts failed.</summary>
    public static readonly TimeSpan Longest = TimeSpan.FromHours(1);

    /// <summary>The most doublings applied; beyond it the pause is already <see cref="Longest"/>.</summary>
    private const int MaxDoublings = 7;

    /// <summary>Gives the pause after a number of failures in a row.</summary>
    /// <param name="failures">How many attempts failed in a row, counting the one that just did; at least one.</param>
    /// <returns>The pause: <see cref="First"/> doubled for every failure after the first, never longer than <see cref="Longest"/>.</returns>
    public static TimeSpan After(int failures)
    {
        var doublings = Math.Clamp(failures - 1, 0, MaxDoublings);
        var delay = TimeSpan.FromTicks(First.Ticks << doublings);
        return delay > Longest ? Longest : delay;
    }
}
