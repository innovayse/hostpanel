namespace Innovayse.Domain.Support;

/// <summary>Priority level for network issues.</summary>
public enum NetworkIssuePriority
{
    /// <summary>Low priority — minimal impact.</summary>
    Low,

    /// <summary>Medium priority — moderate impact.</summary>
    Medium,

    /// <summary>High priority — significant impact.</summary>
    High,

    /// <summary>Critical priority — service outage or major degradation.</summary>
    Critical
}
