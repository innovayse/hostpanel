namespace Innovayse.Infrastructure.Provisioning.CPanel;

using System.Text.Json;
using Microsoft.Extensions.Options;

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper for the cPanel WHM JSON API v1.
/// Handles authentication header injection, request dispatch, and error mapping.
/// </summary>
public sealed class CPanelClient
{
    /// <summary>The underlying HTTP client used to call the WHM API.</summary>
    private readonly HttpClient _http;

    /// <summary>Resolved cPanel configuration settings.</summary>
    private readonly CPanelSettings _settings;

    /// <summary>
    /// Initializes a new instance of <see cref="CPanelClient"/>.
    /// </summary>
    /// <param name="http">The <see cref="HttpClient"/> configured by <c>IHttpClientFactory</c>.</param>
    /// <param name="options">Bound <see cref="CPanelSettings"/> options.</param>
    public CPanelClient(HttpClient http, IOptions<CPanelSettings> options)
    {
        _http = http;
        _settings = options.Value;
    }

    /// <summary>
    /// Creates a new cPanel hosting account via the WHM <c>createacct</c> API function.
    /// </summary>
    /// <param name="domain">Primary domain name for the new account.</param>
    /// <param name="username">cPanel username for the new account.</param>
    /// <param name="password">Initial password for the new account.</param>
    /// <param name="package">Hosting package name as configured on the WHM server.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the WHM API returns a failure status.
    /// </exception>
    public async Task CreateAccountAsync(
        string domain,
        string username,
        string password,
        string package,
        CancellationToken ct)
    {
        var parameters = new Dictionary<string, string>
        {
            ["domain"] = domain,
            ["username"] = username,
            ["password"] = password,
            ["plan"] = package
        };

        await CallApiAsync("createacct", parameters, ct);
    }

    /// <summary>
    /// Suspends an existing cPanel account via the WHM <c>suspendacct</c> API function.
    /// </summary>
    /// <param name="username">cPanel username of the account to suspend.</param>
    /// <param name="reason">Human-readable reason for the suspension.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the WHM API returns a failure status.
    /// </exception>
    public async Task SuspendAccountAsync(string username, string reason, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string>
        {
            ["user"] = username,
            ["reason"] = reason
        };

        await CallApiAsync("suspendacct", parameters, ct);
    }

    /// <summary>
    /// Unsuspends an existing cPanel account via the WHM <c>unsuspendacct</c> API function.
    /// </summary>
    /// <param name="username">cPanel username of the account to unsuspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the WHM API returns a failure status.
    /// </exception>
    public async Task UnsuspendAccountAsync(string username, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string>
        {
            ["user"] = username
        };

        await CallApiAsync("unsuspendacct", parameters, ct);
    }

    /// <summary>
    /// Removes an existing cPanel account via the WHM <c>removeacct</c> API function.
    /// </summary>
    /// <param name="username">cPanel username of the account to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the WHM API returns a failure status.
    /// </exception>
    public async Task RemoveAccountAsync(string username, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string>
        {
            ["username"] = username
        };

        await CallApiAsync("removeacct", parameters, ct);
    }

    /// <summary>
    /// Generates a cPanel single-sign-on URL for the given username via the UAPI
    /// <c>create_user_session</c> endpoint.
    /// </summary>
    /// <param name="username">cPanel username for which to create the SSO session.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A time-limited cPanel SSO URL string.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the WHM API returns a failure status or the URL is absent.
    /// </exception>
    public async Task<string> GetCPanelSsoUrlAsync(string username, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string>
        {
            ["user"] = username,
            ["service"] = "cpaneld"
        };

        var doc = await CallApiAsync("create_user_session", parameters, ct);

        var url = doc.RootElement
            .GetProperty("data")
            .GetProperty("url")
            .GetString();

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException(
                $"cPanel SSO URL was empty for username '{username}'.");
        }

        return url;
    }

    /// <summary>
    /// Calls a WHM JSON API v1 function and returns the parsed <see cref="JsonDocument"/>.
    /// </summary>
    /// <param name="function">The WHM API function name (e.g. <c>createacct</c>).</param>
    /// <param name="parameters">Query-string parameters for the function call.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parsed JSON document of the successful API response.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the HTTP call fails or the API returns a non-success status.
    /// </exception>
    private async Task<JsonDocument> CallApiAsync(
        string function,
        Dictionary<string, string> parameters,
        CancellationToken ct)
    {
        var queryString = string.Join("&",
            parameters.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        var url = $"/json-api/{function}?api.version=1&{queryString}";

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;

        if (root.TryGetProperty("result", out var result))
        {
            if (result.TryGetProperty("status", out var statusEl))
            {
                var status = statusEl.GetInt32();
                if (status != 1)
                {
                    var msg = result.TryGetProperty("statusmsg", out var msgEl)
                        ? msgEl.GetString()
                        : "Unknown WHM API error";
                    throw new InvalidOperationException(
                        $"WHM API '{function}' failed: {msg}");
                }
            }
        }

        return doc;
    }
}
