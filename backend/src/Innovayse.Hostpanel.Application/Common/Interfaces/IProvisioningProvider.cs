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
    /// after a lost answer is safe. An account the panel accepted and is still creating is answered with
    /// <see cref="ProvisioningOutcome.Pending"/>.
    /// </returns>
    public Task<ProvisioningResult> ProvisionAsync(ProvisionAccountOrder req, CancellationToken ct);

    /// <summary>
    /// Suspends an existing hosting account on the provider.
    /// </summary>
    /// <param name="req">Suspension request containing the account reference and reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the provider refuses and the account is not already suspended; an account already
    /// suspended is success, so a retry is safe.
    /// </exception>
    /// <returns>A task that completes when the account is suspended.</returns>
    public Task SuspendAsync(SuspendAccountOrder req, CancellationToken ct);

    /// <summary>
    /// Re-activates a previously suspended hosting account on the provider.
    /// </summary>
    /// <param name="req">Unsuspension request containing the service and the account reference.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the provider refuses and the account is not already active; an account already
    /// active is success, so a retry is safe.
    /// </exception>
    /// <returns>A task that completes when the account is active again.</returns>
    public Task UnsuspendAsync(UnsuspendAccountOrder req, CancellationToken ct);

    /// <summary>
    /// Permanently removes a hosting account from the provider.
    /// </summary>
    /// <param name="req">Termination request containing the account reference and reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the provider refuses and the account is not confirmed gone; an account already
    /// removed is success, so a retry is safe.
    /// </exception>
    /// <returns>A task that completes when the account is removed.</returns>
    public Task TerminateAsync(TerminateAccountOrder req, CancellationToken ct);

    /// <summary>
    /// Retrieves the current login credentials for a provisioned hosting account.
    /// </summary>
    /// <param name="req">The service and the account reference.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="ServiceCredentials"/> record containing username, password,
    /// domain, server IP, and control panel URL.
    /// </returns>
    public Task<ServiceCredentials> GetCredentialsAsync(AccountCredentialsOrder req, CancellationToken ct);

    /// <summary>
    /// Generates a single-sign-on URL for the client to access their control panel directly.
    /// </summary>
    /// <param name="req">The service, the account reference and the address of the browser that will open the link, which only panels that bind a session to it read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A time-limited control-panel sign-in link string.</returns>
    public Task<string> GetPanelSsoUrlAsync(PanelSsoOrder req, CancellationToken ct);

    /// <summary>
    /// Changes the password of a provisioned hosting account.
    /// </summary>
    /// <param name="req">The service, the account reference and the new password to set on the hosting account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the account does not exist or the password change fails.
    /// </exception>
    /// <returns>A task that completes when the password is changed.</returns>
    public Task ChangePasswordAsync(ChangeAccountPasswordOrder req, CancellationToken ct);

    /// <summary>
    /// Changes the hosting package assigned to a provisioned account.
    /// </summary>
    /// <param name="req">The service, the account reference and the name of the new hosting package to assign.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the account does not exist or the package change fails.
    /// </exception>
    /// <returns>A task that completes when the package is changed.</returns>
    public Task ChangePackageAsync(ChangeAccountPackageOrder req, CancellationToken ct);

    /// <summary>
    /// Reads what the panel reports about one hosting account: whether it is active or suspended, its domain, its package and
    /// the IP address it is served on. Read only: nothing is changed on the panel.
    /// </summary>
    /// <param name="req">The service and the account reference.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="Innovayse.Hostpanel.Domain.DomainRuleException">
    /// Thrown with <c>PANEL_OPERATION_NOT_SUPPORTED</c> when the provider cannot read one account, with
    /// <c>PANEL_ACCOUNT_NOT_FOUND</c> when the panel holds no such account and with the panel's own refusal code when it refuses.
    /// </exception>
    /// <returns>The state the panel reports; a field the panel does not report is <see langword="null"/>.</returns>
    public Task<HostedAccountState> GetAccountAsync(AccountStateOrder req, CancellationToken ct);

    /// <summary>
    /// Lists the names of the hosting packages the provider's server offers, so an admin picks one instead of typing it. Read only:
    /// nothing is changed on the panel.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="Innovayse.Hostpanel.Domain.DomainRuleException">
    /// Thrown with <c>PANEL_OPERATION_NOT_SUPPORTED</c> when the provider cannot list its packages, and with the panel's own refusal code
    /// when it refuses.
    /// </exception>
    /// <returns>The package names exactly as the panel knows them, possibly none.</returns>
    public Task<IReadOnlyList<string>> ListPackagesAsync(CancellationToken ct);
}
