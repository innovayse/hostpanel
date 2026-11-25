namespace Innovayse.Hostpanel.Domain.Provisioning;

/// <summary>
/// Request for a single-sign-on link into the control panel of a provisioned hosting service account.
/// </summary>
/// <param name="ServiceId">Internal service identifier from the platform.</param>
/// <param name="ProvisioningRef">Provider-assigned reference for the account.</param>
/// <param name="CallerIp">The address of the browser that will open the link, or <see langword="null"/> when unknown; only panels that bind a session to it read it.</param>
public record PanelSsoOrder(int ServiceId, string ProvisioningRef, string? CallerIp);
