namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Remembers when the next demo reset is due so the public demo endpoint can tell visitors.
/// Held in memory on purpose: the reset wipes the database, so the database cannot hold it.
/// </summary>
public interface IDemoResetSchedule
{
    /// <summary>When the next reset runs; <see langword="null"/> before the first one is scheduled.</summary>
    public DateTimeOffset? NextResetAt { get; }

    /// <summary>Records when the next reset runs.</summary>
    /// <param name="at">The scheduled time.</param>
    public void Set(DateTimeOffset at);
}
