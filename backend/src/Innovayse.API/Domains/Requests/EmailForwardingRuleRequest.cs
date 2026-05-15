namespace Innovayse.API.Domains.Requests;

/// <summary>Request body for creating or updating an email forwarding rule.</summary>
public sealed class EmailForwardingRuleRequest
{
    /// <summary>Source alias (e.g. "info", "support").</summary>
    public required string Source { get; init; }

    /// <summary>Destination email address.</summary>
    public required string Destination { get; init; }

    /// <summary>Whether the rule is active (used in updates only).</summary>
    public bool IsActive { get; init; } = true;
}
