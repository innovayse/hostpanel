using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Providers.CWP7.Models;
using Innovayse.Providers.CWP7.Models.Enums;
using Microsoft.Extensions.Logging;

namespace Innovayse.Providers.CWP7;

/// <summary>
/// Provisioning provider for Control Web Panel 7 (CWP7).
/// Implements <see cref="IProvisioningProvider"/> to integrate with the provisioning pipeline.
/// Created per-server by <see cref="IProvisioningProviderFactory"/>.
/// </summary>
/// <remarks>
/// A refusal is not always a failure. A retry after a lost answer meets the account the first
/// attempt already made, and a suspend, unsuspend or terminate can find the account already in
/// the wanted state. After a refusal the provider asks CWP7 about the account and treats
/// "already as asked" as success; any other refusal still fails. Logs name the account by its
/// reference and the service by its id, never by the panel's own message text or a domain.
/// </remarks>
public sealed class Cwp7ProvisioningProvider : IProvisioningProvider
{
    /// <summary>Domain of the placeholder address CWP7 requires when an account is terminated.</summary>
    private const string TerminatedEmailDomain = "terminated.local";

    /// <summary>Domain of the placeholder address CWP7 requires when a package is changed.</summary>
    private const string ChangeEmailDomain = "change.local";

    /// <summary>Inode limit given to every new account; 0 means unlimited.</summary>
    private const int DefaultInodeLimit = 0;

    /// <summary>Most processes a new account may run at once.</summary>
    private const int DefaultProcessLimit = 40;

    /// <summary>Most files a new account may hold open at once.</summary>
    private const int DefaultOpenFileLimit = 150;

    /// <summary>The failure reason handed back to the caller; the panel's own text is never repeated.</summary>
    private const string CreateFailedReason = "The control panel refused to create the account.";

    /// <summary>CWP7 API client.</summary>
    private readonly Cwp7ApiClient _client;

    /// <summary>Where and how to reach this CWP7 server.</summary>
    private readonly Cwp7Connection _connection;

    /// <summary>Server IP address for account creation.</summary>
    private readonly string _serverIp;

    /// <summary>Logger for structured output.</summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes the provider with server-specific credentials.
    /// </summary>
    /// <param name="connection">Where and how to reach the CWP7 API.</param>
    /// <param name="serverIp">Server IP address for account creation.</param>
    /// <param name="client">CWP7 HTTP API client.</param>
    /// <param name="logger">Logger for request tracing.</param>
    internal Cwp7ProvisioningProvider(Cwp7Connection connection, string serverIp, Cwp7ApiClient client, ILogger logger)
    {
        _connection = connection;
        _serverIp = serverIp;
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new hosting account on the CWP7 server.
    /// </summary>
    /// <param name="req">Provision request with domain, username, password, and package.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Created with the username as the provisioning reference; AlreadyExists with the existing
    /// account's domain when CWP7 refused because the account is already there; Refused otherwise.
    /// </returns>
    public async Task<ProvisioningResult> ProvisionAsync(ProvisionAccountOrder req, CancellationToken ct)
    {
        var account = new Cwp7NewAccount(
            req.DomainName,
            req.Username,
            req.Password,
            req.Package,
            $"{req.Username}@{req.DomainName}",
            DefaultInodeLimit,
            DefaultProcessLimit,
            DefaultOpenFileLimit,
            _serverIp);
        var response = await _client.CreateAccountAsync(_connection, account, ct);

        if (response.IsSuccess)
        {
            _logger.LogInformation("CWP7 account created for service {ServiceId}.", req.ServiceId);
            return ProvisioningResult.Created(req.Username);
        }

        var lookup = await _client.FindAccountAsync(_connection, req.Username, ct);
        if (lookup.Account is { } existing)
        {
            _logger.LogInformation("CWP7 account for service {ServiceId} already exists.", req.ServiceId);
            return ProvisioningResult.AlreadyExists(existing.User, existing.Domain);
        }

        _logger.LogWarning("CWP7 CreateAccount was refused for service {ServiceId}.", req.ServiceId);
        return ProvisioningResult.Refused(CreateFailedReason);
    }

    /// <summary>
    /// Suspends a hosting account on the CWP7 server.
    /// </summary>
    /// <param name="req">Suspend request with provisioning reference (username) and reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when CWP7 refuses and the account is not already suspended.</exception>
    public async Task SuspendAsync(SuspendAccountOrder req, CancellationToken ct)
    {
        var response = await _client.SuspendAccountAsync(_connection, req.ProvisioningRef, ct);

        if (!response.IsSuccess)
        {
            var lookup = await _client.FindAccountAsync(_connection, req.ProvisioningRef, ct);
            if (lookup.Account is not { Suspended: true })
            {
                _logger.LogWarning("CWP7 SuspendAccount was refused for {User}.", req.ProvisioningRef);
                throw new InvalidOperationException("CWP7 suspend failed.");
            }

            _logger.LogInformation("CWP7 account {User} was already suspended.", req.ProvisioningRef);
            return;
        }

        _logger.LogInformation("CWP7 account suspended: {User}", req.ProvisioningRef);
    }

    /// <summary>
    /// Unsuspends a previously suspended hosting account.
    /// </summary>
    /// <param name="provisioningRef">Username of the account to unsuspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when CWP7 refuses and the account is not already active.</exception>
    public async Task UnsuspendAsync(string provisioningRef, CancellationToken ct)
    {
        var response = await _client.UnsuspendAccountAsync(_connection, provisioningRef, ct);

        if (!response.IsSuccess)
        {
            var lookup = await _client.FindAccountAsync(_connection, provisioningRef, ct);
            if (lookup.Account is not { Suspended: false })
            {
                _logger.LogWarning("CWP7 UnsuspendAccount was refused for {User}.", provisioningRef);
                throw new InvalidOperationException("CWP7 unsuspend failed.");
            }

            _logger.LogInformation("CWP7 account {User} was already active.", provisioningRef);
            return;
        }

        _logger.LogInformation("CWP7 account unsuspended: {User}", provisioningRef);
    }

    /// <summary>
    /// Permanently terminates a hosting account on the CWP7 server.
    /// </summary>
    /// <param name="req">Terminate request with provisioning reference (username) and reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when CWP7 refuses and the account still exists or its absence is unconfirmed.</exception>
    public async Task TerminateAsync(TerminateAccountOrder req, CancellationToken ct)
    {
        var response = await _client.TerminateAccountAsync(
            _connection, req.ProvisioningRef, PlaceholderEmail(req.ProvisioningRef, TerminatedEmailDomain), ct);

        if (!response.IsSuccess)
        {
            var lookup = await _client.FindAccountAsync(_connection, req.ProvisioningRef, ct);
            if (lookup.Outcome is not Cwp7LookupOutcome.NotFound)
            {
                _logger.LogWarning("CWP7 TerminateAccount was refused for {User}.", req.ProvisioningRef);
                throw new InvalidOperationException("CWP7 terminate failed.");
            }

            _logger.LogInformation("CWP7 account {User} was already terminated.", req.ProvisioningRef);
            return;
        }

        _logger.LogInformation("CWP7 account terminated: {User}", req.ProvisioningRef);
    }

    /// <summary>
    /// Returns credentials for a CWP7 hosting account.
    /// </summary>
    /// <param name="provisioningRef">Username of the account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Service credentials with username, domain, and server info; CWP7 never reveals a stored password, so none is returned.</returns>
    public Task<ServiceCredentials> GetCredentialsAsync(string provisioningRef, CancellationToken ct)
    {
        var credentials = new ServiceCredentials(
            Username: provisioningRef,
            Password: null,
            Domain: string.Empty,
            ServerIp: _serverIp,
            CpanelUrl: _connection.UserPanelUrl);
        return Task.FromResult(credentials);
    }

    /// <summary>
    /// Generates an auto-login URL for the given user.
    /// </summary>
    /// <param name="provisioningRef">Username to generate control-panel sign-in link for.</param>
    /// <param name="callerIp">The address of the browser that will open the link, or <see langword="null"/> when unknown; only panels that bind a session to it read it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The control-panel sign-in link string.</returns>
    public async Task<string> GetCPanelSsoUrlAsync(string provisioningRef, string? callerIp, CancellationToken ct)
    {
        var response = await _client.GetAutoLoginUrlAsync(_connection, provisioningRef, ct);

        if (!response.IsSuccess)
        {
            _logger.LogWarning("CWP7 AutoLogin failed for {User}.", provisioningRef);
            throw new InvalidOperationException("CWP7 auto-login failed.");
        }

        return response.Message;
    }

    /// <summary>
    /// Changes the password for an existing hosting account.
    /// </summary>
    /// <param name="provisioningRef">Username of the account.</param>
    /// <param name="newPassword">New password to set.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ChangePasswordAsync(string provisioningRef, string newPassword, CancellationToken ct)
    {
        var response = await _client.ChangePasswordAsync(_connection, provisioningRef, newPassword, ct);

        if (!response.IsSuccess)
        {
            _logger.LogWarning("CWP7 ChangePassword failed for {User}.", provisioningRef);
            throw new InvalidOperationException("CWP7 password change failed.");
        }

        _logger.LogInformation("CWP7 password changed for: {User}", provisioningRef);
    }

    /// <summary>
    /// Changes the hosting package for an existing account (upgrade/downgrade).
    /// </summary>
    /// <param name="provisioningRef">Username of the account.</param>
    /// <param name="newPackage">New package name or ID (prefix ID with @).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ChangePackageAsync(string provisioningRef, string newPackage, CancellationToken ct)
    {
        var response = await _client.ChangePackageAsync(
            _connection, provisioningRef, PlaceholderEmail(provisioningRef, ChangeEmailDomain), newPackage, ct);

        if (!response.IsSuccess)
        {
            _logger.LogWarning("CWP7 ChangePackage failed for {User}.", provisioningRef);
            throw new InvalidOperationException("CWP7 package change failed.");
        }

        _logger.LogInformation("CWP7 package changed for {User}.", provisioningRef);
    }

    /// <summary>Builds the placeholder address CWP7's API demands but never mails.</summary>
    /// <param name="user">The account username, used as the local part.</param>
    /// <param name="domain">The reserved placeholder domain.</param>
    /// <returns>The address.</returns>
    private static string PlaceholderEmail(string user, string domain) => $"{user}@{domain}";
}
