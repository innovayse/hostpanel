namespace Innovayse.Domain.Support;

/// <summary>Lifecycle status of a network issue.</summary>
public enum NetworkIssueStatus
{
    /// <summary>Issue has been reported but not yet investigated.</summary>
    Reported,

    /// <summary>Issue is currently being investigated.</summary>
    Investigating,

    /// <summary>Maintenance or fix has been scheduled.</summary>
    Scheduled,

    /// <summary>Issue has been resolved.</summary>
    Resolved
}
