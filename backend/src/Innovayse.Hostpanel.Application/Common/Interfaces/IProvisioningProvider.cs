using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Domain.Provisioning.Enums;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Abstraction over hosting provisioning providers (any hosting control panel).
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
    /// and the provider-assigned reference on success. An account the provider already holds under the
    /// requested username is answered with <see cref="ProvisioningOutcome.AlreadyExists"/>, so a retry
    /// after a lost answer is safe.
    /// </returns>
    Task<ProvisioningResult> ProvisionAsync(ProvisionAccountOrder req, CancellationToken ct);

    /// <summary>
    /// Suspends an existing hosting account on the provider.
    /// </summary>
    /// <param name="req">Suspension request containing the account reference and reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the provider refuses and the account is not already suspended; an account already
    /// suspended is success, so a retry is safe.
    /// </exception>
    Task SuspendAsync(SuspendAccountOrder req, CancellationToken ct);

    /// <summary>
    /// Re-activates a previously suspended hosting account on the provider.
    /// </summary>
    /// <param name="provisioningRef">Provider-assigned reference for the account to unsuspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the provider refuses and the account is not already active; an account already
    /// active is success, so a retry is safe.
    /// </exception>
    Task UnsuspendAsync(string provisioningRef, CancellationToken ct);

    /// <summary>
    /// Permanently removes a hosting account from the provider.
    /// </summary>
    /// <param name="req">Termination request containing the account reference and reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the provider refuses and the account is not confirmed gone; an account already
    /// removed is success, so a retry is safe.
    /// </exception>
    Task TerminateAsync(TerminateAccountOrder req, CancellationToken ct);

    /// <summary>
    /// Retrieves the current login credentials for a provisioned hosting account.
    /// </summary>
    /// <param name="provisioningRef">Provider-assigned reference for the account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="ServiceCredentials"/> record containing username, password,
    /// domain, server IP, and control panel URL.
    /// </returns>
    Task<ServiceCredentials> GetCredentialsAsync(string provisioningRef, CancellationToken ct);

    /// <summary>
    /// Generates a single-sign-on URL for the client to access their control panel directly.
    /// </summary>
    /// <param name="provisioningRef">Provider-assigned reference for the account.</param>
    /// <param name="callerIp">The address of the browser that will open the link, or <see langword="null"/> when unknown; only panels that bind a session to it read it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A time-limited control-panel sign-in link string.</returns>
    Task<string> GetCPanelSsoUrlAsync(string provisioningRef, string? callerIp, CancellationToken ct);

    /// <summary>
    /// Changes the password of a provisioned hosting account.
    /// </summary>
    /// <param name="provisioningRef">Provider-assigned reference for the account.</param>
    /// <param name="newPassword">The new password to set on the hosting account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the account does not exist or the password change fails.
    /// </exception>
    Task ChangePasswordAsync(string provisioningRef, string newPassword, CancellationToken ct);

    /// <summary>
    /// Changes the hosting package assigned to a provisioned account.
    /// </summary>
    /// <param name="provisioningRef">Provider-assigned reference for the account.</param>
    /// <param name="newPackage">The name of the new hosting package to assign.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the account does not exist or the package change fails.
    /// </exception>
    Task ChangePackageAsync(string provisioningRef, string newPackage, CancellationToken ct);
}
