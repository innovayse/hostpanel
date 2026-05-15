namespace Innovayse.Providers.CWP;

using Innovayse.SDK.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provisioning provider plugin for CentOS Web Panel (CWP).
/// Registered as a keyed <c>IProvisioningPlugin</c> under the key "innovayse-cwp".
/// </summary>
public sealed class CwpProvisioningProvider : ProvisioningProviderBase
{
    /// <summary>Plugin identifier — matches the "id" field in plugin.json.</summary>
    private const string PluginId = "innovayse-cwp";

    /// <summary>CWP API client.</summary>
    private readonly CwpApiClient _client;

    /// <summary>CWP API key read from settings at construction time.</summary>
    private readonly string _apiKey;

    /// <summary>CWP server base URL, e.g. "https://cwp.example.com:2031".</summary>
    private readonly string _host;

    /// <summary>
    /// Initializes the provider by reading host, port, and api_key from settings.
    /// </summary>
    /// <param name="configuration">Application configuration — provides plugin settings.</param>
    /// <param name="logger">Logger for structured output.</param>
    /// <param name="loggerFactory">Logger factory used to create a typed logger for <see cref="CwpApiClient"/>.</param>
    public CwpProvisioningProvider(IConfiguration configuration, ILogger<CwpProvisioningProvider> logger, ILoggerFactory loggerFactory)
        : base(PluginId, configuration, logger)
    {
        var host = GetConfig("host") ?? throw new InvalidOperationException("CWP: 'host' setting is required.");
        var port = GetConfig("port");
        var apiKey = GetConfig("api_key") ?? throw new InvalidOperationException("CWP: 'api_key' setting is required.");

        _apiKey = apiKey;
        var resolvedPort = int.TryParse(port, out var p) ? p : 2031;
        _host = $"https://{host}:{resolvedPort}";
        _client = new CwpApiClient(new HttpClient(), loggerFactory.CreateLogger<CwpApiClient>());
    }

    /// <summary>
    /// Creates a new hosting account on the CWP server.
    /// </summary>
    /// <param name="domain">Primary domain for the account.</param>
    /// <param name="username">cPanel username for the account.</param>
    /// <param name="password">Account password.</param>
    /// <param name="package">Hosting package name assigned to the account.</param>
    /// <param name="email">Contact email address for the account owner.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when CWP reports success; false otherwise.</returns>
    public async Task<bool> CreateAccountAsync(
        string domain,
        string username,
        string password,
        string package,
        string email,
        CancellationToken ct)
    {
        var response = await _client.CreateAccountAsync(_host, _apiKey, domain, username, password, package, email, ct);

        if (!response.IsSuccess)
        {
            Logger.LogWarning("CWP CreateAccount failed: {Message}", response.Message);
        }

        return response.IsSuccess;
    }

    /// <summary>
    /// Suspends a hosting account on the CWP server.
    /// </summary>
    /// <param name="username">cPanel username of the account to suspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when CWP reports success; false otherwise.</returns>
    public async Task<bool> SuspendAccountAsync(string username, CancellationToken ct)
    {
        var response = await _client.SuspendAccountAsync(_host, _apiKey, username, ct);

        if (!response.IsSuccess)
        {
            Logger.LogWarning("CWP SuspendAccount failed for {User}: {Message}", username, response.Message);
        }

        return response.IsSuccess;
    }

    /// <summary>
    /// Unsuspends a hosting account on the CWP server.
    /// </summary>
    /// <param name="username">cPanel username of the account to unsuspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when CWP reports success; false otherwise.</returns>
    public async Task<bool> UnsuspendAccountAsync(string username, CancellationToken ct)
    {
        var response = await _client.UnsuspendAccountAsync(_host, _apiKey, username, ct);

        if (!response.IsSuccess)
        {
            Logger.LogWarning("CWP UnsuspendAccount failed for {User}: {Message}", username, response.Message);
        }

        return response.IsSuccess;
    }

    /// <summary>
    /// Permanently terminates a hosting account on the CWP server.
    /// </summary>
    /// <param name="username">cPanel username of the account to terminate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when CWP reports success; false otherwise.</returns>
    public async Task<bool> TerminateAccountAsync(string username, CancellationToken ct)
    {
        var response = await _client.TerminateAccountAsync(_host, _apiKey, username, ct);

        if (!response.IsSuccess)
        {
            Logger.LogWarning("CWP TerminateAccount failed for {User}: {Message}", username, response.Message);
        }

        return response.IsSuccess;
    }
}
