namespace Innovayse.API.Provisioning;

/// <summary>Request body for terminating a hosting service.</summary>
public sealed class TerminateServiceRequest
{
    /// <summary>Gets the human-readable reason for the termination.</summary>
    public required string Reason { get; init; }
}
