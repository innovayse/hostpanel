namespace Innovayse.Hostpanel.Domain.StatusPage.Enums;

/// <summary>Where an incident stands in its timeline, from the first report to the all clear.</summary>
public enum StatusIncidentState
{
    /// <summary>The problem is reported and the cause is being looked for.</summary>
    Investigating = 1,

    /// <summary>The cause is known and a fix is being prepared.</summary>
    Identified = 2,

    /// <summary>A fix is in place and its effect is being watched.</summary>
    Monitoring = 3,

    /// <summary>The problem is over: the incident takes no more updates.</summary>
    Resolved = 4,
}
