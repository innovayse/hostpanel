using System.Globalization;
using System.Text.Json;
using Innovayse.Providers.CWP7.Models;
using Innovayse.SDK.Plugins.HostingPanel;
using Microsoft.Extensions.Logging;

namespace Innovayse.Providers.CWP7;

/// <summary>
/// HTTP client wrapper for the Control Web Panel 7 (CWP7) REST API.
/// Sends form-encoded POST requests to /v1/account and related endpoints.
/// </summary>
/// <remarks>
/// Logs carry operation names, statuses and sizes only. The panel's own message text and the
/// request URLs can echo account data, so neither is written to a log.
/// </remarks>
internal sealed class Cwp7ApiClient : IHostingPanelV7ApiClient
{
    /// <summary>The panel's display name in fixed failure phrases and logs.</summary>
    private const string PanelName = "CWP7";

    /// <summary>
    /// The answer field that holds the payload on success and the panel's sentence on a refusal.
    /// </summary>
    private const string MessageField = "msj";

    /// <summary>The failure text for a session payload that issued nothing for the asked account.</summary>
    private const string NoSessionMessage = "CWP7 issued no session for the account.";

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

    /// <summary>
    /// Creates a new hosting account on the CWP7 server.
    /// </summary>
    /// <param name="connection">Where and how to reach the CWP7 API.</param>
    /// <param name="account">The account to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP7 API response indicating success or failure.</returns>
    public Task<HostingPanelApiWire> CreateAccountAsync(
        Cwp7Connection connection,
        Cwp7NewAccount account,
        CancellationToken ct) =>
        SendAccountAsync(
            connection.BaseUrl,
            new Cwp7AccountRequest
            {
                Key = connection.ApiKey,
                Action = "add",
                Domain = account.Domain,
                User = account.Username,
                Pass = account.Password,
                Package = account.Package,
                Email = account.Email,
                Inode = account.InodeLimit.ToString(CultureInfo.InvariantCulture),
                LimitNproc = account.ProcessLimit.ToString(CultureInfo.InvariantCulture),
                LimitNofile = account.OpenFileLimit.ToString(CultureInfo.InvariantCulture),
                ServerIps = account.ServerIp,
            },
            ct);

    /// <summary>
    /// Suspends an existing hosting account.
    /// </summary>
    /// <param name="connection">Where and how to reach the CWP7 API.</param>
    /// <param name="username">Username of the account to suspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP7 API response indicating success or failure.</returns>
    public Task<HostingPanelApiWire> SuspendAccountAsync(
        Cwp7Connection connection,
        string username,
        CancellationToken ct) =>
        SendAccountAsync(
            connection.BaseUrl,
            new Cwp7AccountRequest
            {
                Key = connection.ApiKey,
                Action = "susp",
                User = username,
            },
            ct);

    /// <summary>
    /// Unsuspends a previously suspended hosting account.
    /// </summary>
    /// <param name="connection">Where and how to reach the CWP7 API.</param>
    /// <param name="username">Username of the account to unsuspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP7 API response indicating success or failure.</returns>
    public Task<HostingPanelApiWire> UnsuspendAccountAsync(
        Cwp7Connection connection,
        string username,
        CancellationToken ct) =>
        SendAccountAsync(
            connection.BaseUrl,
            new Cwp7AccountRequest
            {
                Key = connection.ApiKey,
                Action = "unsp",
                User = username,
            },
            ct);

    /// <summary>
    /// Terminates (permanently deletes) a hosting account.
    /// </summary>
    /// <param name="connection">Where and how to reach the CWP7 API.</param>
    /// <param name="username">Username of the account to delete.</param>
    /// <param name="email">Email associated with the account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP7 API response indicating success or failure.</returns>
    public Task<HostingPanelApiWire> TerminateAccountAsync(
        Cwp7Connection connection,
        string username,
        string email,
        CancellationToken ct) =>
        SendAccountAsync(
            connection.BaseUrl,
            new Cwp7AccountRequest
            {
                Key = connection.ApiKey,
                Action = "del",
                User = username,
                Email = email,
            },
            ct);

    /// <summary>
    /// Changes the password for an existing hosting account.
    /// </summary>
    /// <param name="connection">Where and how to reach the CWP7 API.</param>
    /// <param name="username">Username of the account.</param>
    /// <param name="newPassword">New password to set.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP7 API response indicating success or failure.</returns>
    public async Task<HostingPanelApiWire> ChangePasswordAsync(
        Cwp7Connection connection,
        string username,
        string newPassword,
        CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("key", connection.ApiKey),
            new KeyValuePair<string, string>("action", "udp"),
            new KeyValuePair<string, string>("user", username),
            new KeyValuePair<string, string>("pass", newPassword),
        ]);

        return await PostAsync($"{connection.BaseUrl}/v1/changepass", content, "ChangePassword", ct);
    }

    /// <summary>
    /// Changes the hosting package for an existing account (upgrade/downgrade).
    /// </summary>
    /// <param name="connection">Where and how to reach the CWP7 API.</param>
    /// <param name="username">Username of the account.</param>
    /// <param name="email">Email associated with the account.</param>
    /// <param name="package">New package name or ID (prefix ID with @).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP7 API response indicating success or failure.</returns>
    public async Task<HostingPanelApiWire> ChangePackageAsync(
        Cwp7Connection connection,
        string username,
        string email,
        string package,
        CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("key", connection.ApiKey),
            new KeyValuePair<string, string>("action", "udp"),
            new KeyValuePair<string, string>("user", username),
            new KeyValuePair<string, string>("email", email),
            new KeyValuePair<string, string>("newpackage", package),
        ]);

        return await PostAsync($"{connection.BaseUrl}/v1/changepack", content, "ChangePackage", ct);
    }

    /// <summary>
    /// Asks CWP7 whether an account exists, and what its domain and suspension state are.
    /// </summary>
    /// <param name="connection">Where and how to reach the CWP7 API.</param>
    /// <param name="username">Username of the account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The account when CWP7 reports it, "not found" when CWP7 answers successfully without it,
    /// and "unknown" for any answer that cannot be relied on.
    /// </returns>
    /// <exception cref="HttpRequestException">Thrown when the API request fails.</exception>
    public async Task<Cwp7AccountLookup> FindAccountAsync(
        Cwp7Connection connection,
        string username,
        CancellationToken ct)
    {
        using var content = new Cwp7AccountRequest
        {
            Key = connection.ApiKey,
            Action = "list",
            User = username,
        }.ToFormContent();

        using var response = await _http.PostAsync($"{connection.BaseUrl}/v1/accountdetail", content, ct);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync(ct);
        var lookup = Cwp7AccountLookup.FromBody(raw, username);

        _logger.LogDebug("CWP7 FindAccount answered {Outcome} for {User}.", lookup.Outcome, username);
        return lookup;
    }

    /// <summary>
    /// Generates an auto-login URL for the given user.
    /// </summary>
    /// <param name="connection">Where and how to reach the CWP7 API.</param>
    /// <param name="username">Username to generate control-panel sign-in link for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A response whose message is the control-panel URL on success, or a fixed phrase on failure;
    /// CWP7's own refusal goes to the log only.
    /// </returns>
    /// <remarks>
    /// <c>/v1/user_session</c>, not <c>/v1/autologin</c>: the latter is not a route CWP7 serves,
    /// and its 404 shows the client the plain login form. This endpoint also breaks the shape every other call returns: its
    /// <c>msj</c> is an object carrying one entry per matched account, so it is parsed with its
    /// own model and flattened back into the shared response here.
    /// </remarks>
    public async Task<HostingPanelApiWire> GetAutoLoginUrlAsync(
        Cwp7Connection connection,
        string username,
        CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("key", connection.ApiKey),
            new KeyValuePair<string, string>("action", "list"),
            new KeyValuePair<string, string>("user", username),
        ]);

        using var response = await _http.PostAsync($"{connection.BaseUrl}/v1/user_session", content, ct);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync(ct);
        var session = TryParseUserSession(raw);

        // The account CWP7 was asked about, not merely the first it returned: a server that
        // answered about somebody else must not hand this caller a session on their panel.
        var detail = session?.Payload?.Details?
            .Find(d => string.Equals(d.User, username, StringComparison.OrdinalIgnoreCase));

        if (session is null)
        {
            // Not a session payload, so the body is a refusal or an error page. Only its status and
            // size are logged: the body may echo account data and has no place in the log.
            _logger.LogWarning(
                "CWP7 AutoLogin did not yield a session URL for {User}: status {Status}, body length {Length}.",
                username,
                (int)response.StatusCode,
                raw.Length);

            return HostingPanelApiWire.Failure(NoSessionMessage);
        }

        if (!string.Equals(session.Status, HostingPanelApiWire.OkStatus, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(detail?.Url))
        {
            // A session payload holds live login tokens, possibly for other accounts, so neither
            // the log nor the result repeats it.
            _logger.LogWarning(
                "CWP7 AutoLogin returned status {Status} without a session URL for {User}.",
                session.Status,
                username);

            return HostingPanelApiWire.Failure(NoSessionMessage);
        }

        _logger.LogDebug("CWP7 AutoLogin response: session URL issued for {User}", username);
        return new HostingPanelApiWire { Status = HostingPanelApiWire.OkStatus, Message = detail.Url };
    }

    /// <summary>
    /// Fetches the list of hosting packages to verify connectivity.
    /// Since CWP7 has no version endpoint and Account LIST is not enabled,
    /// we use Packages LIST as a health check.
    /// </summary>
    /// <param name="connection">Where and how to reach the CWP7 API.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of packages configured on the server.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API request fails.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the panel refuses the call, with the panel's own reason in the message for the
    /// server log (callers log it and answer with a fixed code), or when the body cannot be parsed.
    /// </exception>
    public async Task<HostingPanelV7ServerInfo> GetServerInfoAsync(HostingPanelConnection connection, CancellationToken ct)
    {
        using var packagesContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("key", connection.ApiKey),
            new KeyValuePair<string, string>("action", "list"),
        ]);

        using var packagesResponse = await _http.PostAsync($"{connection.BaseUrl}/v1/packages", packagesContent, ct);
        packagesResponse.EnsureSuccessStatusCode();

        using var body = await HostingPanelApiReader.ReadPayloadAsync<JsonDocument>(
            packagesResponse, PanelName, _logger, ct);

        // A refusal (wrong key, address not allowed) is HTTP 200 with "Error" and a sentence in
        // msj, which the list shape cannot hold. It is read first so the log says why the panel
        // refused instead of reporting a JSON conversion error.
        if (body.RootElement.ValueKind == JsonValueKind.Object
            && body.RootElement.TryGetProperty(MessageField, out var refusal)
            && refusal.ValueKind == JsonValueKind.String)
        {
            throw new InvalidOperationException($"{PanelName} API refused the packages list: {refusal.GetString()}");
        }

        var packageList = body.Deserialize<Cwp7PackageListResponse>()
            ?? throw new InvalidOperationException($"{PanelName} API returned an empty packages response.");

        var packagesCount = packageList.Packages?.Count ?? 0;

        _logger.LogDebug("CWP7 GetServerInfo: packages={Count}", packagesCount);

        return new HostingPanelV7ServerInfo(packagesCount);
    }

    /// <summary>Deserializes a user-session body, returning null when it is not that shape.</summary>
    /// <param name="raw">The raw response body.</param>
    /// <returns>The parsed response, or <see langword="null"/> when it could not be read.</returns>
    private Cwp7UserSessionWire? TryParseUserSession(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Cwp7UserSessionWire>(raw);
        }
        catch (JsonException ex)
        {
            // An error from this endpoint arrives as `msj: "<sentence>"`, which is this model's
            // object field holding a string. That is a refusal to report, not a fault to raise.
            _logger.LogDebug(ex, "CWP7 user_session body was not a session payload.");
            return null;
        }
    }

    /// <summary>
    /// Sends a form-encoded POST to the CWP7 /v1/account endpoint and parses the JSON response.
    /// </summary>
    /// <param name="baseUrl">CWP7 server base URL including port.</param>
    /// <param name="request">The request parameters to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed <see cref="HostingPanelApiWire"/>.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the response body is empty.</exception>
    private async Task<HostingPanelApiWire> SendAccountAsync(string baseUrl, Cwp7AccountRequest request, CancellationToken ct)
    {
        using var content = request.ToFormContent();
        return await PostAsync($"{baseUrl}/v1/account", content, request.Action, ct);
    }

    /// <summary>Posts a form and reads the answer through the shared panel response path.</summary>
    /// <param name="url">The absolute endpoint URL; never logged.</param>
    /// <param name="content">The form body.</param>
    /// <param name="operation">The operation name, for the log line.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parsed envelope, or a failure envelope when the body is not JSON.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the response body is empty.</exception>
    private async Task<HostingPanelApiWire> PostAsync(string url, FormUrlEncodedContent content, string operation, CancellationToken ct)
    {
        using var response = await _http.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();

        var result = await HostingPanelApiReader.ReadEnvelopeAsync(response, PanelName, _logger, ct);

        _logger.LogDebug("CWP7 {Operation} answered with status {Status}.", operation, result.Status);
        return result;
    }
}
