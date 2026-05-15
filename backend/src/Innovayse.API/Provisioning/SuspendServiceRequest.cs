namespace Innovayse.API.Provisioning;

/// <summary>Request body for suspending a hosting service.</summary>
public sealed class SuspendServiceRequest
{
    /// <summary>Gets the human-readable reason for the suspension (e.g. "Non-payment").</summary>
    public required string Reason { get; init; }
}
