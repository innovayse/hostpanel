namespace Innovayse.Application.Provisioning.DTOs;

/// <summary>
/// DTO carrying the provisioning details for a hosted service, returned after provisioning completes.
/// </summary>
/// <param name="ServiceId">Internal service identifier from the platform.</param>
/// <param name="ProvisioningRef">Provider-assigned reference identifier for the hosted account.</param>
/// <param name="Status">Current lifecycle status of the provisioned service (e.g. "Active", "Suspended").</param>
/// <param name="ProvisionedAt">UTC timestamp indicating when the service was provisioned.</param>
public record ProvisioningDetailsDto(int ServiceId, string ProvisioningRef, string Status, DateTimeOffset ProvisionedAt);
