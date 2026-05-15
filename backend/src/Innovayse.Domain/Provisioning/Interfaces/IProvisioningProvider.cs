namespace Innovayse.Domain.Provisioning.Interfaces;

/// <summary>
/// Abstraction over hosting provisioning providers (cPanel WHM, Plesk, etc.).
/// Implementations live in the Infrastructure layer and are registered via DI.
/// </summary>
public interface IProvisioningProvider
{
    /// <summary>
    /// Creates a new hosting account on the provider.
    /// </summary>
    /// <param name="req">Details of the service to provision.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="ProvisioningResult"/> indicating success or failure,
    /// and the provider-assigned reference on success.
    /// </returns>
    Task<ProvisioningResult> ProvisionAsync(ProvisionRequest req, CancellationToken ct);

    /// <summary>
    /// Suspends an existing hosting account on the provider.
    /// </summary>
    /// <param name="req">Suspension request containing the account reference and reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the account does not exist or is already suspended.
    /// </exception>
    Task SuspendAsync(SuspendRequest req, CancellationToken ct);

    /// <summary>
    /// Re-activates a previously suspended hosting account on the provider.
    /// </summary>
    /// <param name="provisioningRef">Provider-assigned reference for the account to unsuspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the account does not exist or is not currently suspended.
    /// </exception>
    Task UnsuspendAsync(string provisioningRef, CancellationToken ct);

    /// <summary>
    /// Permanently removes a hosting account from the provider.
    /// </summary>
    /// <param name="req">Termination request containing the account reference and reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the account does not exist on the provider.
    /// </exception>
    Task TerminateAsync(TerminateRequest req, CancellationToken ct);

    /// <summary>
    /// Retrieves the current login credentials for a provisioned hosting account.
    /// </summary>
    /// <param name="provisioningRef">Provider-assigned reference for the account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="ServiceCredentials"/> record containing username, password,
    /// domain, server IP, and cPanel URL.
    /// </returns>
    Task<ServiceCredentials> GetCredentialsAsync(string provisioningRef, CancellationToken ct);

    /// <summary>
    /// Generates a single-sign-on URL for the client to access their cPanel directly.
    /// </summary>
    /// <param name="provisioningRef">Provider-assigned reference for the account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A time-limited cPanel SSO URL string.</returns>
    Task<string> GetCPanelSsoUrlAsync(string provisioningRef, CancellationToken ct);
}
