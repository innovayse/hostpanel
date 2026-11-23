namespace Innovayse.Hostpanel.Domain.Provisioning;

/// <summary>
/// Request to move a provisioned hosting service account to another package on the provider.
/// </summary>
/// <param name="ServiceId">Internal service identifier from the platform.</param>
/// <param name="ProvisioningRef">Provider-assigned reference for the account.</param>
/// <param name="NewPackage">The name of the new hosting package to assign.</param>
public record ChangeAccountPackageOrder(int ServiceId, string ProvisioningRef, string NewPackage);
