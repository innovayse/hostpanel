namespace Innovayse.Hostpanel.Domain.Provisioning;

/// <summary>
/// Request to suspend an active hosting service account on the provider.
/// </summary>
/// <param name="ServiceId">Internal service identifier from the platform.</param>
/// <param name="ProvisioningRef">Provider-assigned reference for the account to suspend.</param>
/// <param name="Reason">
/// Human-readable reason for the suspension (e.g. "Non-payment"), or <see langword="null"/> when none was given.
/// </param>
public record SuspendAccountOrder(int ServiceId, string ProvisioningRef, string? Reason = null);
