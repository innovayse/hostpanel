namespace Innovayse.Domain.Services.Interfaces;

/// <summary>
/// Abstraction over a hosting control panel provisioning API (cPanel WHM, Plesk, etc.).
/// Implemented in Infrastructure — never called directly from Application.
/// </summary>
public interface IProvisioningProvider
{
    /// <summary>
    /// Provisions a new hosting account or resource for a client service.
    /// </summary>
    /// <param name="clientId">The ID of the client the service belongs to.</param>
    /// <param name="productId">The ID of the product being provisioned.</param>
    /// <param name="billingCycle">Billing cycle: "monthly" or "annual".</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An opaque reference string to identify the provisioned resource.</returns>
    Task<string> ProvisionAsync(int clientId, int productId, string billingCycle, CancellationToken ct);

    /// <summary>
    /// Suspends a previously provisioned service.
    /// </summary>
    /// <param name="provisioningRef">The reference returned by <see cref="ProvisionAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SuspendAsync(string provisioningRef, CancellationToken ct);

    /// <summary>
    /// Re-activates a previously suspended provisioned service.
    /// </summary>
    /// <param name="provisioningRef">The reference returned by <see cref="ProvisionAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UnsuspendAsync(string provisioningRef, CancellationToken ct);

    /// <summary>
    /// Permanently terminates a provisioned service and releases its resources.
    /// </summary>
    /// <param name="provisioningRef">The reference returned by <see cref="ProvisionAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    Task TerminateAsync(string provisioningRef, CancellationToken ct);
}
