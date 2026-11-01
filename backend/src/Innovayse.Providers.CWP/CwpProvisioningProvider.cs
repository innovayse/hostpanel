using Innovayse.Providers.CWP.Models;
using Innovayse.Providers.CWP.Models.Enums;
using Innovayse.SDK.Base;
using Innovayse.SDK.Plugins.HostingPanel;
using Innovayse.SDK.Plugins.Provisioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Innovayse.Providers.CWP;

/// <summary>
/// Provisioning provider plugin for CentOS Web Panel (CWP).
/// Registered as a keyed <c>IProvisioningPlugin</c> under the key "innovayse-cwp".
/// </summary>
public sealed class CwpProvisioningProvider : ProvisioningProviderBase
{
    /// <summary>Plugin identifier: matches the "id" field in plugin.json.</summary>
    private const string PluginId = "innovayse-cwp";

    /// <summary>CWP API client.</summary>
    private readonly CwpApiClient _client;

    /// <summary>Where and how to reach the CWP server, read from settings at construction time.</summary>
    private readonly CwpConnection _connection;

    /// <summary>
    /// Initializes the provider by reading host, port, and api_key from settings.
    /// </summary>
    /// <remarks>
    /// The <see cref="HttpClient"/> comes from <paramref name="httpClientFactory"/> under
    /// <see cref="CwpApiClient.HttpClientName"/>, like every other HTTP client in this codebase.
    /// That gives it the configured timeout, pooled sockets and, above all, the no-retry
    /// resilience pipeline. Every CWP account call is a write, so a retried request would
    /// re-create or re-terminate a hosting account.
    /// </remarks>
    /// <param name="configuration">Application configuration: provides plugin settings.</param>
    /// <param name="logger">Logger for structured output.</param>
    /// <param name="loggerFactory">Logger factory used to create a typed logger for <see cref="CwpApiClient"/>.</param>
    /// <param name="httpClientFactory">Factory supplying the pooled, resilience-configured CWP HTTP client.</param>
    public CwpProvisioningProvider(
        IConfiguration configuration,
        ILogger<CwpProvisioningProvider> logger,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory)
        : base(PluginId, configuration, logger)
    {
        var host = GetConfig("host") ?? throw new InvalidOperationException("CWP: 'host' setting is required.");
        var port = GetConfig("port");
        var apiKey = GetConfig("api_key") ?? throw new InvalidOperationException("CWP: 'api_key' setting is required.");

        var resolvedPort = int.TryParse(port, out var p) ? p : CwpApiClient.DefaultPort;
        _connection = new CwpConnection(HostingPanelAddress.BaseUrl(host, resolvedPort), apiKey);
        _client = new CwpApiClient(
            httpClientFactory.CreateClient(CwpApiClient.HttpClientName),
            loggerFactory.CreateLogger<CwpApiClient>());
    }

    /// <summary>
    /// Creates a new hosting account on the CWP server.
    /// </summary>
    /// <param name="account">The account to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Succeeded when CWP created the account; AlreadyExists, with the existing account's domain,
    /// when CWP refused because the username is already taken; Refused otherwise.
    /// </returns>
    public async Task<CwpAccountResult> CreateAccountAsync(CwpNewAccount account, CancellationToken ct)
    {
        var response = await _client.CreateAccountAsync(_connection, account, ct);

        if (response.IsSuccess)
        {
            return new CwpAccountResult(CwpAccountOutcome.Succeeded, null);
        }

        var existing = await TryFindAccountAsync(account.Username, ct);
        if (existing is not null)
        {
            return new CwpAccountResult(CwpAccountOutcome.AlreadyExists, existing.Domain);
        }

        Logger.LogWarning("CWP CreateAccount was refused for {User}.", account.Username);
        return new CwpAccountResult(CwpAccountOutcome.Refused, null);
    }

    /// <summary>
    /// Suspends a hosting account on the CWP server.
    /// </summary>
    /// <param name="username">cPanel username of the account to suspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Succeeded, AlreadyInState when the account is already suspended, or Refused.</returns>
    public async Task<CwpAccountResult> SuspendAccountAsync(string username, CancellationToken ct)
    {
        var response = await _client.SuspendAccountAsync(_connection, username, ct);

        if (response.IsSuccess)
        {
            return new CwpAccountResult(CwpAccountOutcome.Succeeded, null);
        }

        var existing = await TryFindAccountAsync(username, ct);
        if (existing is { IsSuspended: true })
        {
            return new CwpAccountResult(CwpAccountOutcome.AlreadyInState, null);
        }

        Logger.LogWarning("CWP SuspendAccount was refused for {User}.", username);
        return new CwpAccountResult(CwpAccountOutcome.Refused, null);
    }

    /// <summary>
    /// Unsuspends a hosting account on the CWP server.
    /// </summary>
    /// <param name="username">cPanel username of the account to unsuspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Succeeded, AlreadyInState when the account is already active, or Refused.</returns>
    public async Task<CwpAccountResult> UnsuspendAccountAsync(string username, CancellationToken ct)
    {
        var response = await _client.UnsuspendAccountAsync(_connection, username, ct);

        if (response.IsSuccess)
        {
            return new CwpAccountResult(CwpAccountOutcome.Succeeded, null);
        }

        var existing = await TryFindAccountAsync(username, ct);
        if (existing is { IsSuspended: false })
        {
            return new CwpAccountResult(CwpAccountOutcome.AlreadyInState, null);
        }

        Logger.LogWarning("CWP UnsuspendAccount was refused for {User}.", username);
        return new CwpAccountResult(CwpAccountOutcome.Refused, null);
    }

    /// <summary>
    /// Permanently terminates a hosting account on the CWP server.
    /// </summary>
    /// <param name="username">cPanel username of the account to terminate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Succeeded, AlreadyInState when CWP lists no such account, or Refused.</returns>
    public async Task<CwpAccountResult> TerminateAccountAsync(string username, CancellationToken ct)
    {
        var response = await _client.TerminateAccountAsync(_connection, username, ct);

        if (response.IsSuccess)
        {
            return new CwpAccountResult(CwpAccountOutcome.Succeeded, null);
        }

        if (await IsAccountAbsentAsync(username, ct))
        {
            return new CwpAccountResult(CwpAccountOutcome.AlreadyInState, null);
        }

        Logger.LogWarning("CWP TerminateAccount was refused for {User}.", username);
        return new CwpAccountResult(CwpAccountOutcome.Refused, null);
    }

    /// <summary>Looks an account up after a refusal, treating a listing that fails as "state unknown".</summary>
    /// <param name="username">The account to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The account's entry, or <see langword="null"/> when it is not listed or the listing failed.</returns>
    private async Task<CwpAccountEntry?> TryFindAccountAsync(string username, CancellationToken ct)
    {
        try
        {
            return await _client.FindAccountAsync(_connection, username, ct);
        }
        catch (InvalidOperationException)
        {
            // No usable listing says nothing about the account; the caller reports the refusal.
            return null;
        }
    }

    /// <summary>Confirms CWP lists no such account, so a refused removal can be read as "already removed".</summary>
    /// <param name="username">The account that was to be removed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> only when the listing succeeded and did not contain the account.</returns>
    private async Task<bool> IsAccountAbsentAsync(string username, CancellationToken ct)
    {
        try
        {
            return await _client.FindAccountAsync(_connection, username, ct) is null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
