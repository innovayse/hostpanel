namespace Innovayse.Providers.CWP7;

using System.Net.Http.Json;
using Innovayse.Providers.CWP7.Models;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Logging;

/// <summary>
/// HTTP client wrapper for the Control Web Panel 7 (CWP7) REST API.
/// Sends form-encoded POST requests to /v1/account and related endpoints.
/// </summary>
internal sealed class Cwp7ApiClient : ICwp7ApiClient
{
    /// <summary>Default CWP7 API port when not specified in configuration.</summary>
    private const int DefaultPort = 2304;

    /// <summary>The underlying HTTP client used for all requests.</summary>
    private readonly HttpClient _http;

    /// <summary>Structured logger for request and response events.</summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new <see cref="Cwp7ApiClient"/> with a pooled HTTP client.
    /// </summary>
    /// <param name="http">Pre-configured HTTP client injected by IHttpClientFactory.</param>
    /// <param name="logger">Logger for request tracing and error reporting.</param>
    public Cwp7ApiClient(HttpClient http, ILogger<Cwp7ApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>Maximum number of characters of a non-JSON body carried into the error message.</summary>
    /// <remarks>
    /// A CWP7 error page is a full HTML document. The first few hundred characters hold the
    /// <c>&lt;title&gt;</c> and the error text an operator needs; the rest is markup that would
    /// only bloat every log line and every provisioning failure message.
    /// </remarks>
    private const int MaxErrorBodyLength = 500;

    /// <summary>
    /// Reads the HTTP response body and parses it as <see cref="Cwp7ApiResponse"/>.
    /// </summary>
    /// <remarks>
    /// CWP7 answers errors with HTTP 200 and an HTML body, so <c>EnsureSuccessStatusCode</c>
    /// upstream cannot catch them. A body that is not the JSON envelope is therefore a
    /// <em>failure</em>: it is returned as a response with status "Error" carrying the start of
    /// the body, so the caller's <c>IsSuccess</c> check refuses the operation. This used to be
    /// reported as a success, which turned a CWP7 error page into a provisioned account.
    /// </remarks>
    /// <param name="response">The HTTP response message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed CWP7 response, or a failure response when the body is not JSON.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the response body is empty.</exception>
    private async Task<Cwp7ApiResponse> ParseResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var raw = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("CWP7 API returned an empty response body.");
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Cwp7ApiResponse>(raw)
                ?? throw new InvalidOperationException("CWP7 API returned null JSON.");
        }
        catch (System.Text.Json.JsonException ex)
        {
            var excerpt = raw.Length > MaxErrorBodyLength
                ? raw[..MaxErrorBodyLength]
                : raw;

            _logger.LogError(
                ex,
                "CWP7 API returned a non-JSON response body; treating the call as failed. Body starts: {Body}",
                excerpt);

            return new Cwp7ApiResponse
            {
                Status = "Error",
                Msj = $"CWP7 returned a non-JSON response: {excerpt}",
            };
        }
    }

    /// <summary>
    /// Creates a new hosting account on the CWP7 server.
    /// </summary>
    /// <param name="host">CWP7 server base URL including port.</param>
    /// <param name="apiKey">CWP7 API key for authentication.</param>
    /// <param name="domain">Primary domain for the account.</param>
    /// <param name="username">Username to create.</param>
    /// <param name="password">Account password.</param>
    /// <param name="package">Hosting package name.</param>
    /// <param name="email">Contact email address.</param>
    /// <param name="inode">Inode limit (0 for unlimited).</param>
    /// <param name="limitNproc">Process limit.</param>
    /// <param name="limitNofile">Open files limit.</param>
    /// <param name="serverIps">Server IP address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP7 API response indicating success or failure.</returns>
    public Task<Cwp7ApiResponse> CreateAccountAsync(
        string host,
        string apiKey,
        string domain,
        string username,
        string password,
        string package,
        string email,
        string inode,
        string limitNproc,
        string limitNofile,
        string serverIps,
        CancellationToken ct) =>
        SendAccountAsync(
            host,
            new Cwp7AccountRequest
            {
                Key = apiKey,
                Action = "add",
                Domain = domain,
                User = username,
                Pass = password,
                Package = package,
                Email = email,
                Inode = inode,
                LimitNproc = limitNproc,
                LimitNofile = limitNofile,
                ServerIps = serverIps,
            },
            ct);

    /// <summary>
    /// Suspends an existing hosting account.
    /// </summary>
    /// <param name="host">CWP7 server base URL including port.</param>
    /// <param name="apiKey">CWP7 API key for authentication.</param>
    /// <param name="username">Username of the account to suspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP7 API response indicating success or failure.</returns>
    public Task<Cwp7ApiResponse> SuspendAccountAsync(
        string host,
        string apiKey,
        string username,
        CancellationToken ct) =>
        SendAccountAsync(
            host,
            new Cwp7AccountRequest
            {
                Key = apiKey,
                Action = "susp",
                User = username,
            },
            ct);

    /// <summary>
    /// Unsuspends a previously suspended hosting account.
    /// </summary>
    /// <param name="host">CWP7 server base URL including port.</param>
    /// <param name="apiKey">CWP7 API key for authentication.</param>
    /// <param name="username">Username of the account to unsuspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP7 API response indicating success or failure.</returns>
    public Task<Cwp7ApiResponse> UnsuspendAccountAsync(
        string host,
        string apiKey,
        string username,
        CancellationToken ct) =>
        SendAccountAsync(
            host,
            new Cwp7AccountRequest
            {
                Key = apiKey,
                Action = "unsp",
                User = username,
            },
            ct);

    /// <summary>
    /// Terminates (permanently deletes) a hosting account.
    /// </summary>
    /// <param name="host">CWP7 server base URL including port.</param>
    /// <param name="apiKey">CWP7 API key for authentication.</param>
    /// <param name="username">Username of the account to delete.</param>
    /// <param name="email">Email associated with the account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP7 API response indicating success or failure.</returns>
    public Task<Cwp7ApiResponse> TerminateAccountAsync(
        string host,
        string apiKey,
        string username,
        string email,
        CancellationToken ct) =>
        SendAccountAsync(
            host,
            new Cwp7AccountRequest
            {
                Key = apiKey,
                Action = "del",
                User = username,
                Email = email,
            },
            ct);

    /// <summary>
    /// Changes the password for an existing hosting account.
    /// </summary>
    /// <param name="host">CWP7 server base URL including port.</param>
    /// <param name="apiKey">CWP7 API key for authentication.</param>
    /// <param name="username">Username of the account.</param>
    /// <param name="newPassword">New password to set.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP7 API response indicating success or failure.</returns>
    public async Task<Cwp7ApiResponse> ChangePasswordAsync(
        string host,
        string apiKey,
        string username,
        string newPassword,
        CancellationToken ct)
    {
        var url = $"{host}/v1/changepass";
        _logger.LogDebug("CWP7 API request: ChangePassword url={Url} user={User}", url, username);

        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("key", apiKey),
            new KeyValuePair<string, string>("action", "udp"),
            new KeyValuePair<string, string>("user", username),
            new KeyValuePair<string, string>("pass", newPassword),
        ]);

        using var response = await _http.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();

        var result = await ParseResponseAsync(response, ct);

        _logger.LogDebug("CWP7 ChangePassword response: status={Status} message={Message}", result.Status, result.Message);
        return result;
    }

    /// <summary>
    /// Changes the hosting package for an existing account (upgrade/downgrade).
    /// </summary>
    /// <param name="host">CWP7 server base URL including port.</param>
    /// <param name="apiKey">CWP7 API key for authentication.</param>
    /// <param name="username">Username of the account.</param>
    /// <param name="email">Email associated with the account.</param>
    /// <param name="package">New package name or ID (prefix ID with @).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP7 API response indicating success or failure.</returns>
    public async Task<Cwp7ApiResponse> ChangePackageAsync(
        string host,
        string apiKey,
        string username,
        string email,
        string package,
        CancellationToken ct)
    {
        var url = $"{host}/v1/changepack";
        _logger.LogDebug("CWP7 API request: ChangePackage url={Url} user={User} package={Package}", url, username, package);

        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("key", apiKey),
            new KeyValuePair<string, string>("action", "udp"),
            new KeyValuePair<string, string>("user", username),
            new KeyValuePair<string, string>("email", email),
            new KeyValuePair<string, string>("newpackage", package),
        ]);

        using var response = await _http.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();

        var result = await ParseResponseAsync(response, ct);

        _logger.LogDebug("CWP7 ChangePackage response: status={Status} message={Message}", result.Status, result.Message);
        return result;
    }

    /// <summary>
    /// Generates an auto-login (SSO) URL for the given user.
    /// </summary>
    /// <param name="host">CWP7 server base URL including port.</param>
    /// <param name="apiKey">CWP7 API key for authentication.</param>
    /// <param name="username">Username to generate SSO link for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A response whose message is the control-panel URL on success, or CWP7's own refusal on
    /// failure.
    /// </returns>
    /// <remarks>
    /// <c>/v1/user_session</c>, not <c>/v1/autologin</c> — the latter is not a route CWP7 serves
    /// and answered 404 for every client who ever pressed the button, which the page then showed
    /// as the plain login form. This endpoint also breaks the shape every other call returns: its
    /// <c>msj</c> is an object carrying one entry per matched account, so it is parsed with its
    /// own model and flattened back into the shared response here.
    /// </remarks>
    public async Task<Cwp7ApiResponse> GetAutoLoginUrlAsync(
        string host,
        string apiKey,
        string username,
        CancellationToken ct)
    {
        var url = $"{host}/v1/user_session";
        _logger.LogDebug("CWP7 API request: AutoLogin url={Url} user={User}", url, username);

        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("key", apiKey),
            new KeyValuePair<string, string>("action", "list"),
            new KeyValuePair<string, string>("user", username),
        ]);

        using var response = await _http.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync(ct);
        var session = TryParseUserSession(raw);

        // The account CWP7 was asked about, not merely the first it returned: a server that
        // answered about somebody else must not hand this caller a session on their panel.
        var detail = session?.Payload?.Details?
            .Find(d => string.Equals(d.User, username, StringComparison.OrdinalIgnoreCase));

        if (session is null
            || !string.Equals(session.Status, "OK", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(detail?.Url))
        {
            var excerpt = raw.Length > MaxErrorBodyLength ? raw[..MaxErrorBodyLength] : raw;
            _logger.LogWarning(
                "CWP7 AutoLogin did not yield a session URL for {User}. Body starts: {Body}",
                username,
                excerpt);

            return new Cwp7ApiResponse { Status = "Error", Msj = excerpt };
        }

        _logger.LogDebug("CWP7 AutoLogin response: session URL issued for {User}", username);
        return new Cwp7ApiResponse { Status = "OK", Msj = detail.Url };
    }

    /// <summary>Deserializes a user-session body, returning null when it is not that shape.</summary>
    /// <param name="raw">The raw response body.</param>
    /// <returns>The parsed response, or <see langword="null"/> when it could not be read.</returns>
    private Cwp7UserSessionResponse? TryParseUserSession(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Cwp7UserSessionResponse>(raw);
        }
        catch (System.Text.Json.JsonException ex)
        {
            // An error from this endpoint arrives as `msj: "<sentence>"`, which is this model's
            // object field holding a string. That is a refusal to report, not a fault to raise.
            _logger.LogDebug(ex, "CWP7 user_session body was not a session payload.");
            return null;
        }
    }

    /// <summary>
    /// Fetches the list of hosting packages to verify connectivity.
    /// Since CWP7 has no version endpoint and Account LIST is not enabled,
    /// we use Packages LIST as a health check.
    /// </summary>
    /// <param name="host">CWP7 server hostname or IP.</param>
    /// <param name="port">CWP7 API port (e.g. "2304").</param>
    /// <param name="apiKey">CWP7 API key for authentication.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of packages configured on the server.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API request fails.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the response body cannot be parsed.</exception>
    public async Task<int> GetServerInfoAsync(
        string host, string port, string apiKey, CancellationToken ct)
    {
        var resolvedPort = int.TryParse(port, out var p) ? p : DefaultPort;
        var baseUrl = $"https://{host}:{resolvedPort}";

        var packagesUrl = $"{baseUrl}/v1/packages";
        _logger.LogDebug("CWP7 GetServerInfo: fetching packages from {Url}", packagesUrl);

        using var packagesContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("key", apiKey),
            new KeyValuePair<string, string>("action", "list"),
        ]);

        using var packagesResponse = await _http.PostAsync(packagesUrl, packagesContent, ct);
        packagesResponse.EnsureSuccessStatusCode();

        var packageList = await packagesResponse.Content.ReadFromJsonAsync<Cwp7PackageListResponse>(ct)
            ?? throw new InvalidOperationException("CWP7 API returned an empty packages response.");

        var packagesCount = packageList.Packages?.Count ?? 0;

        _logger.LogDebug("CWP7 GetServerInfo: packages={Count}", packagesCount);

        return packagesCount;
    }

    /// <summary>
    /// Sends a form-encoded POST to the CWP7 /v1/account endpoint and parses the JSON response.
    /// </summary>
    /// <param name="baseUrl">CWP7 server base URL including port.</param>
    /// <param name="request">The request parameters to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed <see cref="Cwp7ApiResponse"/>.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the response body cannot be parsed.</exception>
    private async Task<Cwp7ApiResponse> SendAccountAsync(string baseUrl, Cwp7AccountRequest request, CancellationToken ct)
    {
        var url = $"{baseUrl}/v1/account";
        _logger.LogDebug("CWP7 API request: action={Action} url={Url}", request.Action, url);

        using var content = request.ToFormContent();
        using var response = await _http.PostAsync(url, content, ct);

        response.EnsureSuccessStatusCode();

        var result = await ParseResponseAsync(response, ct);

        _logger.LogDebug(
            "CWP7 API response: status={Status} message={Message}",
            result.Status,
            result.Message);

        return result;
    }
}
