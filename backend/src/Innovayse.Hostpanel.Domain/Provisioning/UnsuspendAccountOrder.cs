namespace Innovayse.Hostpanel.Domain.Provisioning;

/// <summary>
/// Request to re-activate a suspended hosting service account on the provider.
/// </summary>
/// <param name="ServiceId">Internal service identifier from the platform.</param>
/// <param name="ProvisioningRef">Provider-assigned reference for the account to unsuspend.</param>
public record UnsuspendAccountOrder(int ServiceId, string ProvisioningRef);
