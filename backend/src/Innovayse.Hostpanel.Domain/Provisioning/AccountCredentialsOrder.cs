namespace Innovayse.Hostpanel.Domain.Provisioning;

/// <summary>
/// Request to read the sign-in credentials of a provisioned hosting service account from the provider.
/// </summary>
/// <param name="ServiceId">Internal service identifier from the platform.</param>
/// <param name="ProvisioningRef">Provider-assigned reference for the account.</param>
public record AccountCredentialsOrder(int ServiceId, string ProvisioningRef);
