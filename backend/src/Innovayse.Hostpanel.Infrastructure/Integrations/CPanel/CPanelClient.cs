using System.Text.Json;
using Innovayse.SDK.Plugins.HostingPanel;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.CPanel;

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper for the cPanel WHM JSON API v1.
/// Handles request dispatch and error mapping; the authorization header is set by the
/// <c>IHttpClientFactory</c> registration.
/// </summary>
public sealed class CPanelClient
{
    /// <summary>The underlying HTTP client used to call the WHM API.</summary>
    private readonly HttpClient _http;

    /// <summary>Records the panel's own refusal text, which never travels further than the log.</summary>
    private readonly ILogger<CPanelClient> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CPanelClient"/>.
    /// </summary>
    /// <param name="http">The <see cref="HttpClient"/> configured by <c>IHttpClientFactory</c>.</param>
    /// <param name="logger">Records the panel's own refusal text, which never travels further than the log.</param>
    public CPanelClient(HttpClient http, ILogger<CPanelClient> logger)
    {
        _http = http;
        _logger = logger;
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

        using var doc = await CallApiAsync("createacct", parameters, ct);
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

        using var doc = await CallApiAsync("suspendacct", parameters, ct);
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

        using var doc = await CallApiAsync("unsuspendacct", parameters, ct);
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
            // WHM's removeacct names the account "user", not "username": the same spelling
            // every other account function on this client uses. "username" is silently ignored
            // and the call reports success without removing anything.
            ["user"] = username
        };

        using var doc = await CallApiAsync("removeacct", parameters, ct);
    }

    /// <summary>
    /// Changes the password for an existing cPanel account via the WHM <c>passwd</c> API function.
    /// </summary>
    /// <param name="username">cPanel username of the account whose password will be changed.</param>
    /// <param name="newPassword">The new password to set for the account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the WHM API returns a failure status.
    /// </exception>
    public async Task ChangePasswordAsync(string username, string newPassword, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string>
        {
            ["user"] = username,
            ["password"] = newPassword
        };

        using var doc = await CallApiAsync("passwd", parameters, ct);
    }

    /// <summary>
    /// Changes the hosting package for an existing cPanel account via the WHM <c>changepackage</c> API function.
    /// </summary>
    /// <param name="username">cPanel username of the account whose package will be changed.</param>
    /// <param name="newPackage">The name of the new hosting package to assign.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the WHM API returns a failure status.
    /// </exception>
    public async Task ChangePackageAsync(string username, string newPackage, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string>
        {
            ["user"] = username,
            ["pkg"] = newPackage
        };

        using var doc = await CallApiAsync("changepackage", parameters, ct);
    }

    /// <summary>
    /// Reads one account's primary domain and suspension state via the WHM <c>accountsummary</c>
    /// API function.
    /// </summary>
    /// <param name="username">cPanel username of the account to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The account's summary, or <see langword="null"/> when WHM holds no such account.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the WHM API returns a failure status, so an unreachable answer is never read
    /// as "no such account".
    /// </exception>
    public async Task<WhmAccountSummary?> FindAccountAsync(string username, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string>
        {
            ["user"] = username
        };

        using var doc = await CallApiAsync("accountsummary", parameters, ct);

        if (!doc.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind is not JsonValueKind.Object
            || !data.TryGetProperty("acct", out var accounts)
            || accounts.ValueKind is not JsonValueKind.Array)
        {
            return null;
        }

        foreach (var account in accounts.EnumerateArray())
        {
            var user = ReadString(account, "user");
            if (!string.Equals(user, username, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return new WhmAccountSummary(user!, ReadString(account, "domain") ?? string.Empty, IsSuspended(account));
        }

        return null;
    }

    /// <summary>
    /// Generates a cPanel single-sign-on URL for the given username via the UAPI
    /// <c>create_user_session</c> endpoint.
    /// </summary>
    /// <param name="username">cPanel username for which to create the sign-on provider session.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A time-limited control-panel sign-in link string.</returns>
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

        using var doc = await CallApiAsync("create_user_session", parameters, ct);

        var url = doc.RootElement.TryGetProperty("data", out var data)
            && data.ValueKind is JsonValueKind.Object
            ? ReadString(data, "url")
            : null;

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException(
                $"cPanel SSO URL was empty for username '{username}'.");
        }

        return url;
    }

    /// <summary>Reads a string property, tolerating a missing or non-string value.</summary>
    /// <param name="element">The JSON object holding the property.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The string, or <see langword="null"/> when absent or not a string.</returns>
    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind is JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.ValueKind is JsonValueKind.String
                ? value.GetString()
                : null;

    /// <summary>Reads whether an account entry carries the suspended flag.</summary>
    /// <param name="account">The account entry from <c>accountsummary</c>.</param>
    /// <returns><see langword="true"/> when WHM marks the account suspended; an absent flag is <see langword="false"/>.</returns>
    private static bool IsSuspended(JsonElement account) =>
        account.TryGetProperty("suspended", out var flag) && HostingPanelApiReader.IsFlagSet(flag);

    /// <summary>
    /// Calls a WHM JSON API v1 function and returns the parsed <see cref="JsonDocument"/>, which
    /// the caller disposes.
    /// </summary>
    /// <remarks>
    /// The request asks for <c>api.version=1</c>, whose envelope is
    /// <c>{"metadata":{"result":0|1,"reason":"text"},"data":{}}</c>. <c>result</c> is <c>1</c> for
    /// success and <c>0</c> for a refusal, and <c>reason</c> carries WHM's own sentence. The
    /// <c>result.status</c> / <c>statusmsg</c> pair is the API v0 shape and never appears in a
    /// v1 body, so it is not read.
    /// <para>
    /// Every parameter travels in a form-encoded POST body, never in the query string: account
    /// passwords are parameters of <c>createacct</c> and <c>passwd</c>, and a URL ends up in
    /// proxy and access logs. The URL carries only the function name.
    /// </para>
    /// </remarks>
    /// <param name="function">The WHM API function name (e.g. <c>createacct</c>).</param>
    /// <param name="parameters">Form fields for the function call.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parsed JSON document of the successful API response.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the API answers with <c>metadata.result</c> of <c>0</c>, or when the response
    /// carries no <c>metadata.result</c> at all and therefore cannot be confirmed as a success.
    /// </exception>
    private async Task<JsonDocument> CallApiAsync(
        string function,
        Dictionary<string, string> parameters,
        CancellationToken ct)
    {
        var fields = new List<KeyValuePair<string, string>>(parameters) { new("api.version", "1") };
        using var content = new FormUrlEncodedContent(fields);

        using var response = await _http.PostAsync($"/json-api/{function}", content, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);

        try
        {
            EnsureSucceeded(function, doc);
        }
        catch
        {
            doc.Dispose();
            throw;
        }

        return doc;
    }

    /// <summary>
    /// Reads the WHM API v1 <c>metadata</c> envelope and throws when the call was refused.
    /// </summary>
    /// <param name="function">The WHM API function name, used in the failure message.</param>
    /// <param name="doc">The parsed response body.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>metadata.result</c> is not <c>1</c>, or when the envelope is absent:
    /// an unrecognised body is a failure to report, never an unverified success.
    /// </exception>
    private void EnsureSucceeded(string function, JsonDocument doc)
    {
        var root = doc.RootElement;

        if (root.ValueKind is not JsonValueKind.Object
            || !root.TryGetProperty("metadata", out var metadata)
            || metadata.ValueKind is not JsonValueKind.Object
            || !metadata.TryGetProperty("result", out var resultEl))
        {
            throw new InvalidOperationException(
                $"WHM API '{function}' returned a response with no 'metadata.result' envelope; " +
                "the outcome of the call cannot be determined.");
        }

        // WHM writes result as the number 1/0 in most builds and as the string "1"/"0" in some,
        // so both spellings are accepted rather than one of them reading as a refusal.
        var succeeded = resultEl.ValueKind switch
        {
            JsonValueKind.Number => resultEl.TryGetInt32(out var n) && n == 1,
            JsonValueKind.String => resultEl.GetString() == "1",
            JsonValueKind.True => true,
            _ => false,
        };

        if (succeeded)
        {
            return;
        }

        // WHM's own sentence can name accounts and domains, so it goes to the log and not into
        // the exception message that callers pass on.
        _logger.LogWarning(
            "WHM API '{Function}' refused the request: {Reason}",
            function,
            ReadString(metadata, "reason") ?? "no reason given");

        throw new InvalidOperationException($"WHM API '{function}' refused the request.");
    }
}
