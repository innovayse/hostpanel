namespace Innovayse.Hostpanel.Domain.StatusPage.Enums;

/// <summary>Where a maintenance window stands. Worked out from the schedule and the cancellation, never stored.</summary>
public enum StatusMaintenanceState
{
    /// <summary>The window has not started yet.</summary>
    Scheduled = 1,

    /// <summary>The window is under way: its components show as under maintenance.</summary>
    InProgress = 2,

    /// <summary>The window ended.</summary>
    Completed = 3,

    /// <summary>The window was called off before it ended.</summary>
    Cancelled = 4,
}
