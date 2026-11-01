using Innovayse.Providers.CWP.Models;
using Innovayse.SDK.Plugins.HostingPanel;
using Microsoft.Extensions.Logging;

namespace Innovayse.Providers.CWP;

/// <summary>
/// HTTP client wrapper for the CentOS Web Panel (CWP) REST API.
/// Sends form-encoded POST requests to the /v1/account endpoint.
/// </summary>
internal sealed class CwpApiClient : IHostingPanelApiClient
{
    /// <summary>Default CWP API port when not specified in configuration.</summary>
    /// <remarks>
    /// This is the <em>API</em> port, which is what every consumer of this constant needs: the
    /// only thing built from it is a URL under <c>https://host:port/v1/</c>. 2031 is the CWP panel's
    /// own HTTPS UI and answers nothing under <c>/v1/</c>. <see cref="CwpProvisioningProvider"/>
    /// and this client both read this one constant.
    /// <para>
    /// The number itself lives on <see cref="IHostingPanelApiClient.DefaultApiPort"/>, the contract
    /// both this assembly and the Application layer can see. This alias stays so the consumers
    /// inside this provider keep reading a name local to them, but it cannot drift from
    /// the fallback the Application layer applies.
    /// </para>
    /// </remarks>
    internal const int DefaultPort = IHostingPanelApiClient.DefaultApiPort;

    /// <summary>
    /// Name of the <c>IHttpClientFactory</c> registration that serves this client.
    /// </summary>
    /// <remarks>
    /// Declared here, beside the client it configures, so the composition root in
    /// <c>Innovayse.Hostpanel.Infrastructure.DependencyInjection</c> and
    /// <see cref="CwpProvisioningProvider"/> cannot drift apart over a string literal.
    /// </remarks>
    internal const string HttpClientName = "Cwp";

    /// <summary>The panel's display name in fixed failure phrases and logs.</summary>
    private const string PanelName = "CWP";

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
    /// <param name="connection">Where and how to reach the CWP server.</param>
    /// <param name="account">The account to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP API response indicating success or failure.</returns>
    public Task<HostingPanelApiWire> CreateAccountAsync(
        CwpConnection connection,
        CwpNewAccount account,
        CancellationToken ct) =>
        SendAsync(
            connection.BaseUrl,
            new CwpAccountRequest
            {
                Key = connection.ApiKey,
                Action = "add",
                Domain = account.Domain,
                User = account.Username,
                Pass = account.Password,
                Package = account.Package,
                Email = account.Email,
            },
            ct);

    /// <summary>
    /// Suspends an existing hosting account.
    /// </summary>
    /// <param name="connection">Where and how to reach the CWP server.</param>
    /// <param name="username">cPanel username of the account to suspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP API response indicating success or failure.</returns>
    public Task<HostingPanelApiWire> SuspendAccountAsync(
        CwpConnection connection,
        string username,
        CancellationToken ct) =>
        SendAsync(
            connection.BaseUrl,
            new CwpAccountRequest
            {
                Key = connection.ApiKey,
                Action = "susp",
                User = username,
            },
            ct);

    /// <summary>
    /// Unsuspends a previously suspended hosting account.
    /// </summary>
    /// <param name="connection">Where and how to reach the CWP server.</param>
    /// <param name="username">cPanel username of the account to unsuspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP API response indicating success or failure.</returns>
    public Task<HostingPanelApiWire> UnsuspendAccountAsync(
        CwpConnection connection,
        string username,
        CancellationToken ct) =>
        SendAsync(
            connection.BaseUrl,
            new CwpAccountRequest
            {
                Key = connection.ApiKey,
                Action = "unsp",
                User = username,
            },
            ct);

    /// <summary>
    /// Terminates (permanently deletes) a hosting account.
    /// </summary>
    /// <param name="connection">Where and how to reach the CWP server.</param>
    /// <param name="username">cPanel username of the account to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP API response indicating success or failure.</returns>
    public Task<HostingPanelApiWire> TerminateAccountAsync(
        CwpConnection connection,
        string username,
        CancellationToken ct) =>
        SendAsync(
            connection.BaseUrl,
            new CwpAccountRequest
            {
                Key = connection.ApiKey,
                Action = "del",
                User = username,
            },
            ct);

    /// <summary>
    /// Looks one account up in the CWP account list.
    /// </summary>
    /// <param name="connection">Where and how to reach the CWP server.</param>
    /// <param name="username">Username of the account to find.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The account's entry, or <see langword="null"/> when CWP lists no such account.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API request fails.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when CWP does not answer the listing successfully, so a failed listing is never
    /// read as "no such account".
    /// </exception>
    public async Task<CwpAccountEntry?> FindAccountAsync(
        CwpConnection connection,
        string username,
        CancellationToken ct)
    {
        using var content = new CwpAccountRequest
        {
            Key = connection.ApiKey,
            Action = "list",
        }.ToFormContent();

        using var response = await _http.PostAsync($"{connection.BaseUrl}/v1/account", content, ct);
        response.EnsureSuccessStatusCode();

        var list = await HostingPanelApiReader.ReadPayloadAsync<CwpAccountListResponse>(response, PanelName, _logger, ct);
        if (!string.Equals(list.Status, HostingPanelApiWire.OkStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("CWP did not list the accounts.");
        }

        return list.Accounts?.Find(a => string.Equals(a.User, username, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Fetches server metadata including total account count and CWP version string.
    /// Calls <c>/v1/account</c> (action=list) for accounts and <c>/v1/version</c> for the version.
    /// </summary>
    /// <param name="connection">Where and how to reach the CWP API.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The total account count and CWP version string.</returns>
    /// <exception cref="HttpRequestException">Thrown when either API request fails.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the response body cannot be parsed.</exception>
    public async Task<HostingPanelServerInfo> GetServerInfoAsync(HostingPanelConnection connection, CancellationToken ct)
    {
        var apiKey = connection.ApiKey;
        var baseUrl = connection.BaseUrl;

        using var accountContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("key", apiKey),
            new KeyValuePair<string, string>("action", "list"),
        ]);

        using var accountResponse = await _http.PostAsync($"{baseUrl}/v1/account", accountContent, ct);
        accountResponse.EnsureSuccessStatusCode();

        var accountList = await HostingPanelApiReader.ReadPayloadAsync<CwpAccountListResponse>(
            accountResponse, PanelName, _logger, ct);

        var accountsCount = accountList.Accounts?.Count ?? 0;

        using var versionContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("key", apiKey),
            new KeyValuePair<string, string>("action", "get"),
        ]);

        using var versionResponse = await _http.PostAsync($"{baseUrl}/v1/version", versionContent, ct);
        versionResponse.EnsureSuccessStatusCode();

        var versionResult = await HostingPanelApiReader.ReadPayloadAsync<CwpVersionResponse>(
            versionResponse, PanelName, _logger, ct);

        var cwpVersion = versionResult.Version ?? "unknown";

        _logger.LogDebug(
            "CWP GetServerInfo: accounts={Count} version={Version}",
            accountsCount,
            cwpVersion);

        return new HostingPanelServerInfo(accountsCount, cwpVersion);
    }

    /// <summary>
    /// Sends a form-encoded POST to the CWP /v1/account endpoint and parses the JSON response.
    /// </summary>
    /// <param name="baseUrl">CWP server base URL including port, e.g. "https://cwp.example.com:2304".</param>
    /// <param name="request">The request parameters to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed <see cref="HostingPanelApiWire"/>, or a failure envelope when the body is not JSON.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the response body is empty.</exception>
    private async Task<HostingPanelApiWire> SendAsync(string baseUrl, CwpAccountRequest request, CancellationToken ct)
    {
        using var content = request.ToFormContent();
        using var response = await _http.PostAsync($"{baseUrl}/v1/account", content, ct);

        response.EnsureSuccessStatusCode();

        // The one panel response path: a 200 carrying an HTML page reads as a failure, not a crash.
        var result = await HostingPanelApiReader.ReadEnvelopeAsync(response, PanelName, _logger, ct);

        _logger.LogDebug("CWP API response: action={Action} status={Status}", request.Action, result.Status);

        return result;
    }
}
