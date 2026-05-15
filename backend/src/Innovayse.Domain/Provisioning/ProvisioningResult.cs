namespace Innovayse.Domain.Provisioning;

/// <summary>
/// Result of a provisioning operation returned by <see cref="Interfaces.IProvisioningProvider"/>.
/// </summary>
/// <param name="Success">Indicates whether the operation completed successfully.</param>
/// <param name="ProvisioningRef">
/// Provider-assigned reference identifier for the provisioned account; null on failure.
/// </param>
/// <param name="ErrorMessage">Human-readable error description; null on success.</param>
public record ProvisioningResult(bool Success, string? ProvisioningRef, string? ErrorMessage);
