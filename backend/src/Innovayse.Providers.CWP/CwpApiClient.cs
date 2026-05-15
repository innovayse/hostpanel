namespace Innovayse.Providers.CWP;

using System.Net.Http.Json;
using Innovayse.Providers.CWP.Models;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Logging;

/// <summary>
/// HTTP client wrapper for the CentOS Web Panel (CWP) REST API.
/// Sends form-encoded POST requests to the /v1/account endpoint.
/// </summary>
internal sealed class CwpApiClient : ICwpApiClient
{
    /// <summary>Default CWP API port when not specified in configuration.</summary>
    private const int DefaultPort = 2304;

    /// <summary>The underlying HTTP client used for all requests.</summary>
    private readonly HttpClient _http;

    /// <summary>Structured logger for request and response events.</summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new <see cref="CwpApiClient"/> with a pooled HTTP client.
    /// </summary>
    /// <param name="http">Pre-configured HTTP client injected by IHttpClientFactory.</param>
    /// <param name="logger">Logger for request tracing and error reporting.</param>
    public CwpApiClient(HttpClient http, ILogger<CwpApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new hosting account on the CWP server.
    /// </summary>
    /// <param name="host">CWP server base URL including port, e.g. "https://cwp.example.com:2031".</param>
    /// <param name="apiKey">CWP API key for authentication.</param>
    /// <param name="domain">Primary domain for the account.</param>
    /// <param name="username">cPanel username.</param>
    /// <param name="password">Account password.</param>
    /// <param name="package">Hosting package name.</param>
    /// <param name="email">Contact email address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP API response indicating success or failure.</returns>
    public Task<CwpApiResponse> CreateAccountAsync(
        string host,
        string apiKey,
        string domain,
        string username,
        string password,
        string package,
        string email,
        CancellationToken ct) =>
        SendAsync(
            host,
            new CwpAccountRequest
            {
                Key = apiKey,
                Action = "add",
                Domain = domain,
                User = username,
                Pass = password,
                Package = package,
                Email = email,
            },
            ct);

    /// <summary>
    /// Suspends an existing hosting account.
    /// </summary>
    /// <param name="host">CWP server base URL including port, e.g. "https://cwp.example.com:2031".</param>
    /// <param name="apiKey">CWP API key for authentication.</param>
    /// <param name="username">cPanel username of the account to suspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP API response indicating success or failure.</returns>
    public Task<CwpApiResponse> SuspendAccountAsync(
        string host,
        string apiKey,
        string username,
        CancellationToken ct) =>
        SendAsync(
            host,
            new CwpAccountRequest
            {
                Key = apiKey,
                Action = "susp",
                User = username,
            },
            ct);

    /// <summary>
    /// Unsuspends a previously suspended hosting account.
    /// </summary>
    /// <param name="host">CWP server base URL including port, e.g. "https://cwp.example.com:2031".</param>
    /// <param name="apiKey">CWP API key for authentication.</param>
    /// <param name="username">cPanel username of the account to unsuspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP API response indicating success or failure.</returns>
    public Task<CwpApiResponse> UnsuspendAccountAsync(
        string host,
        string apiKey,
        string username,
        CancellationToken ct) =>
        SendAsync(
            host,
            new CwpAccountRequest
            {
                Key = apiKey,
                Action = "unsp",
                User = username,
            },
            ct);

    /// <summary>
    /// Terminates (permanently deletes) a hosting account.
    /// </summary>
    /// <param name="host">CWP server base URL including port, e.g. "https://cwp.example.com:2031".</param>
    /// <param name="apiKey">CWP API key for authentication.</param>
    /// <param name="username">cPanel username of the account to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP API response indicating success or failure.</returns>
    public Task<CwpApiResponse> TerminateAccountAsync(
        string host,
        string apiKey,
        string username,
        CancellationToken ct) =>
        SendAsync(
            host,
            new CwpAccountRequest
            {
                Key = apiKey,
                Action = "del",
                User = username,
            },
            ct);

    /// <summary>
    /// Fetches server metadata including total account count and CWP version string.
    /// Calls <c>/v1/account</c> (action=list) for accounts and <c>/v1/version</c> for the version.
    /// </summary>
    /// <param name="host">CWP server hostname or IP.</param>
    /// <param name="port">CWP API port (e.g. "2031").</param>
    /// <param name="apiKey">CWP API key for authentication.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple with total account count and CWP version string.</returns>
    /// <exception cref="HttpRequestException">Thrown when either API request fails.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the response body cannot be parsed.</exception>
    public async Task<(int AccountsCount, string CwpVersion)> GetServerInfoAsync(
        string host, string port, string apiKey, CancellationToken ct)
    {
        var resolvedPort = int.TryParse(port, out var p) ? p : DefaultPort;
        var baseUrl = $"https://{host}:{resolvedPort}";

        // Fetch account list for count
        var accountUrl = $"{baseUrl}/v1/account";
        _logger.LogDebug("CWP GetServerInfo: fetching accounts from {Url}", accountUrl);

        using var accountContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("key", apiKey),
            new KeyValuePair<string, string>("action", "list"),
        ]);

        using var accountResponse = await _http.PostAsync(accountUrl, accountContent, ct);
        accountResponse.EnsureSuccessStatusCode();

        var accountList = await accountResponse.Content.ReadFromJsonAsync<CwpAccountListResponse>(ct)
            ?? throw new InvalidOperationException("CWP API returned an empty account list response.");

        var accountsCount = accountList.Accounts?.Count ?? 0;

        // Fetch version
        var versionUrl = $"{baseUrl}/v1/version";
        _logger.LogDebug("CWP GetServerInfo: fetching version from {Url}", versionUrl);

        using var versionContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("key", apiKey),
            new KeyValuePair<string, string>("action", "get"),
        ]);

        using var versionResponse = await _http.PostAsync(versionUrl, versionContent, ct);
        versionResponse.EnsureSuccessStatusCode();

        var versionResult = await versionResponse.Content.ReadFromJsonAsync<CwpVersionResponse>(ct)
            ?? throw new InvalidOperationException("CWP API returned an empty version response.");

        var cwpVersion = versionResult.Version ?? "unknown";

        _logger.LogDebug(
            "CWP GetServerInfo: accounts={Count} version={Version}",
            accountsCount,
            cwpVersion);

        return (accountsCount, cwpVersion);
    }

    /// <summary>
    /// Sends a form-encoded POST to the CWP /v1/account endpoint and parses the JSON response.
    /// </summary>
    /// <param name="baseUrl">CWP server base URL including port, e.g. "https://cwp.example.com:2031".</param>
    /// <param name="request">The request parameters to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed <see cref="CwpApiResponse"/>.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the response body cannot be parsed.</exception>
    private async Task<CwpApiResponse> SendAsync(string baseUrl, CwpAccountRequest request, CancellationToken ct)
    {
        var url = $"{baseUrl}/v1/account";
        _logger.LogDebug("CWP API request: action={Action} url={Url}", request.Action, url);

        using var content = request.ToFormContent();
        using var response = await _http.PostAsync(url, content, ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CwpApiResponse>(ct)
            ?? throw new InvalidOperationException("CWP API returned an empty response body.");

        _logger.LogDebug(
            "CWP API response: status={Status} message={Message}",
            result.Status,
            result.Message);

        return result;
    }
}
