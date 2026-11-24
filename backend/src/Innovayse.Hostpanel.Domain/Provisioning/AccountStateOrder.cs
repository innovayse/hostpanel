namespace Innovayse.Hostpanel.Domain.Provisioning;

/// <summary>
/// Request to read the current state of a provisioned hosting service account from the provider. Read only.
/// </summary>
/// <param name="ServiceId">Internal service identifier from the platform.</param>
/// <param name="ProvisioningRef">Provider-assigned reference for the account.</param>
public record AccountStateOrder(int ServiceId, string ProvisioningRef);
