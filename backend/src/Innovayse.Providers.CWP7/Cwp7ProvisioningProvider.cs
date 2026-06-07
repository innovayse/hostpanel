namespace Innovayse.Providers.CWP7;

using Innovayse.Domain.Provisioning;
using Innovayse.Domain.Provisioning.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provisioning provider for Control Web Panel 7 (CWP7).
/// Implements <see cref="IProvisioningProvider"/> to integrate with the provisioning pipeline.
/// Created per-server by <see cref="IProvisioningProviderFactory"/>.
/// </summary>
public sealed class Cwp7ProvisioningProvider : IProvisioningProvider
{
    /// <summary>CWP7 API client.</summary>
    private readonly Cwp7ApiClient _client;

    /// <summary>CWP7 API key (access hash) for this server.</summary>
    private readonly string _apiKey;

    /// <summary>CWP7 server base URL, e.g. "https://srv4.example.com:2304".</summary>
    private readonly string _host;

    /// <summary>Server IP address for account creation.</summary>
    private readonly string _serverIp;

    /// <summary>Logger for structured output.</summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes the provider with server-specific credentials.
    /// </summary>
    /// <param name="host">CWP7 server base URL including port.</param>
    /// <param name="apiKey">CWP7 API key (access hash).</param>
    /// <param name="serverIp">Server IP address for account creation.</param>
    /// <param name="client">CWP7 HTTP API client.</param>
    /// <param name="logger">Logger for request tracing.</param>
    internal Cwp7ProvisioningProvider(string host, string apiKey, string serverIp, Cwp7ApiClient client, ILogger logger)
    {
        _host = host;
        _apiKey = apiKey;
        _serverIp = serverIp;
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new hosting account on the CWP7 server.
    /// </summary>
    /// <param name="req">Provision request with domain, username, password, and package.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Provisioning result with the username as the provisioning reference.</returns>
    public async Task<ProvisioningResult> ProvisionAsync(ProvisionRequest req, CancellationToken ct)
    {
        var response = await _client.CreateAccountAsync(
            _host, _apiKey, req.DomainName, req.Username, req.Password, req.Package,
            $"{req.Username}@{req.DomainName}",
            inode: "0", limitNproc: "40", limitNofile: "150", serverIps: _serverIp, ct);

        if (!response.IsSuccess)
        {
            _logger.LogWarning("CWP7 CreateAccount failed for {User}@{Domain}: {Message}",
                req.Username, req.DomainName, response.Message);
            return new ProvisioningResult(false, null, response.Message);
        }

        _logger.LogInformation("CWP7 account created: {User}@{Domain}", req.Username, req.DomainName);
        return new ProvisioningResult(true, req.Username, null);
    }

    /// <summary>
    /// Suspends a hosting account on the CWP7 server.
    /// </summary>
    /// <param name="req">Suspend request with provisioning reference (username) and reason.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SuspendAsync(SuspendRequest req, CancellationToken ct)
    {
        var response = await _client.SuspendAccountAsync(_host, _apiKey, req.ProvisioningRef, ct);

        if (!response.IsSuccess)
        {
            _logger.LogWarning("CWP7 SuspendAccount failed for {User}: {Message}", req.ProvisioningRef, response.Message);
            throw new InvalidOperationException($"CWP7 suspend failed: {response.Message}");
        }

        _logger.LogInformation("CWP7 account suspended: {User}", req.ProvisioningRef);
    }

    /// <summary>
    /// Unsuspends a previously suspended hosting account.
    /// </summary>
    /// <param name="provisioningRef">Username of the account to unsuspend.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task UnsuspendAsync(string provisioningRef, CancellationToken ct)
    {
        var response = await _client.UnsuspendAccountAsync(_host, _apiKey, provisioningRef, ct);

        if (!response.IsSuccess)
        {
            _logger.LogWarning("CWP7 UnsuspendAccount failed for {User}: {Message}", provisioningRef, response.Message);
            throw new InvalidOperationException($"CWP7 unsuspend failed: {response.Message}");
        }

        _logger.LogInformation("CWP7 account unsuspended: {User}", provisioningRef);
    }

    /// <summary>
    /// Permanently terminates a hosting account on the CWP7 server.
    /// </summary>
    /// <param name="req">Terminate request with provisioning reference (username) and reason.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task TerminateAsync(TerminateRequest req, CancellationToken ct)
    {
        var response = await _client.TerminateAccountAsync(
            _host, _apiKey, req.ProvisioningRef, $"{req.ProvisioningRef}@terminated.local", ct);

        if (!response.IsSuccess)
        {
            _logger.LogWarning("CWP7 TerminateAccount failed for {User}: {Message}", req.ProvisioningRef, response.Message);
            throw new InvalidOperationException($"CWP7 terminate failed: {response.Message}");
        }

        _logger.LogInformation("CWP7 account terminated: {User}", req.ProvisioningRef);
    }

    /// <summary>
    /// Returns credentials for a CWP7 hosting account.
    /// </summary>
    /// <param name="provisioningRef">Username of the account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Service credentials with username, domain, and server info.</returns>
    public Task<ServiceCredentials> GetCredentialsAsync(string provisioningRef, CancellationToken ct)
    {
        var credentials = new ServiceCredentials(
            Username: provisioningRef,
            Password: "**hidden**",
            Domain: string.Empty,
            ServerIp: _serverIp,
            CpanelUrl: _host.Replace(":2304", ":2083"));
        return Task.FromResult(credentials);
    }

    /// <summary>
    /// Generates an auto-login (SSO) URL for the given user.
    /// </summary>
    /// <param name="provisioningRef">Username to generate SSO link for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The SSO URL string.</returns>
    public async Task<string> GetCPanelSsoUrlAsync(string provisioningRef, CancellationToken ct)
    {
        var response = await _client.GetAutoLoginUrlAsync(_host, _apiKey, provisioningRef, ct);

        if (!response.IsSuccess)
        {
            _logger.LogWarning("CWP7 AutoLogin failed for {User}: {Message}", provisioningRef, response.Message);
            throw new InvalidOperationException($"CWP7 auto-login failed: {response.Message}");
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
        var response = await _client.ChangePasswordAsync(_host, _apiKey, provisioningRef, newPassword, ct);

        if (!response.IsSuccess)
        {
            _logger.LogWarning("CWP7 ChangePassword failed for {User}: {Message}", provisioningRef, response.Message);
            throw new InvalidOperationException($"CWP7 password change failed: {response.Message}");
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
            _host, _apiKey, provisioningRef, $"{provisioningRef}@change.local", newPackage, ct);

        if (!response.IsSuccess)
        {
            _logger.LogWarning("CWP7 ChangePackage failed for {User}: {Message}", provisioningRef, response.Message);
            throw new InvalidOperationException($"CWP7 package change failed: {response.Message}");
        }

        _logger.LogInformation("CWP7 package changed for {User} to {Package}", provisioningRef, newPackage);
    }
}
