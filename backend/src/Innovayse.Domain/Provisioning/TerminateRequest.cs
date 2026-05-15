namespace Innovayse.Domain.Provisioning;

/// <summary>
/// Request to permanently terminate and remove a hosting service account from the provider.
/// </summary>
/// <param name="ServiceId">Internal service identifier from the platform.</param>
/// <param name="ProvisioningRef">Provider-assigned reference for the account to terminate.</param>
/// <param name="Reason">Human-readable reason for the termination.</param>
public record TerminateRequest(int ServiceId, string ProvisioningRef, string Reason);
