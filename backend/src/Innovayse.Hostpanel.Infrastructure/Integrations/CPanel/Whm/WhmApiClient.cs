using System.Globalization;
using System.Text.Json;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Models;
using Microsoft.Extensions.Logging;
using Polly.Timeout;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm;

/// <summary>
/// Client for WHM API 1 (<c>/json-api/&lt;function&gt;?api.version=1</c>), one server per call.
/// </summary>
/// <remarks>
/// <para>
/// Every call is a POST to <c>/json-api/&lt;function&gt;?api.version=1</c> whose arguments travel in a
/// form encoded body, which WHM API 1 accepts like a query string, and authenticates with
/// <c>Authorization: whm user:token</c>. The body keeps a password (<c>createacct</c>, <c>passwd</c>)
/// out of the URL, which WHM's access log, a proxy and the HTTP stack's own diagnostics record, so the
/// URL holds the function name and nothing else. No URL is logged either, and the named HTTP client is
/// registered without the factory's request logging; the resilience pipeline's telemetry names its
/// strategies and outcomes, never the request.
/// </para>
/// <para>
/// WHM reports a refusal inside an HTTP 200 body, with <c>metadata.result</c> 0. Every operation
/// answers a <see cref="WhmReply"/> or a <see cref="WhmLookup"/> instead of throwing, so the
/// provider decides what a refusal means. WHM's <c>metadata.reason</c> is never read into a reply
/// or a log line: it can carry a username or a domain.
/// </para>
/// <para>
/// A transport failure, an error status or a body that is not a WHM reply is
/// <see cref="WhmReply.Unreadable"/>, and a lookup that meets one is
/// <see cref="Models.Enums.WhmLookupOutcome.Unknown"/>, never "not found", so a failed read can
/// never convince a caller that an account is gone.
/// </para>
/// </remarks>
public sealed class WhmApiClient
{
    /// <summary>The path every WHM API 1 function is served under.</summary>
    private const string ApiPath = "/json-api/";

    /// <summary>The scheme WHM expects before <c>user:token</c> in the <c>Authorization</c> header.</summary>
    private const string AuthScheme = "whm";

    /// <summary>
    /// The most of a reply body this client buffers. An unfiltered <c>listaccts</c> on a very large
    /// server is a few megabytes; anything past this is a misbehaving or hostile endpoint, and it is
    /// refused instead of being held in memory.
    /// </summary>
    public const long MaxResponseBytes = 16 * 1024 * 1024;

    /// <summary>The HTTP client, resolved by name so the WHM timeout and resilience profile apply.</summary>
    private readonly HttpClient _http;

    /// <summary>Records failed calls by function name only, without URLs or WHM's own text.</summary>
    private readonly ILogger<WhmApiClient> _logger;

    /// <summary>Initializes a new instance of the <see cref="WhmApiClient"/> class.</summary>
    /// <param name="http">The HTTP client, resolved by name so the WHM timeout and resilience profile apply.</param>
    /// <param name="logger">Records failed calls by function name only, without URLs or WHM's own text.</param>
    public WhmApiClient(HttpClient http, ILogger<WhmApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>Reads the WHM version, which also proves the token works.</summary>
    /// <param name="connection">The server to ask.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The server's details.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WHM refuses or cannot be read.</exception>
    public async Task<WhmServerInfo> GetVersionAsync(WhmConnection connection, CancellationToken ct)
    {
        var reply = await CallAsync(connection, "version", [], ct);
        if (!reply.IsSuccess)
        {
            throw new InvalidOperationException(reply.IsRefusal ? "WHM refused the version call." : "WHM version could not be read.");
        }

        return new WhmServerInfo(ReadString(reply.Data, "version"));
    }

    /// <summary>Counts the accounts the token can see with an unfiltered <c>listaccts</c>.</summary>
    /// <param name="connection">The server to ask.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of accounts.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WHM refuses or the answer cannot be read.</exception>
    public async Task<int> CountAccountsAsync(WhmConnection connection, CancellationToken ct)
    {
        var reply = await CallAsync(connection, "listaccts", [], ct);
        if (!reply.IsSuccess || reply.Data is not { ValueKind: JsonValueKind.Object } data)
        {
            throw new InvalidOperationException("WHM account list could not be read.");
        }

        if (!data.TryGetProperty("acct", out var accounts))
        {
            return 0;
        }

        return accounts.ValueKind == JsonValueKind.Array
            ? accounts.GetArrayLength()
            : throw new InvalidOperationException("WHM account list could not be read.");
    }

    /// <summary>Looks an account up by username with <c>listaccts</c>, exact match on the user.</summary>
    /// <param name="connection">The server to ask.</param>
    /// <param name="user">The account's username.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The account, a positive "not found", or "unknown".</returns>
    /// <remarks>
    /// <c>listaccts</c> answers success with an empty list for a user that does not exist, which is
    /// a positive "not found". <c>accountsummary</c> refuses instead, and a refusal cannot be told
    /// from any other, so it is not used for lookups.
    /// </remarks>
    public async Task<WhmLookup> FindAccountAsync(WhmConnection connection, string user, CancellationToken ct)
    {
        var reply = await CallAsync(
            connection,
            "listaccts",
            [new("search", user), new("searchtype", "user"), new("searchmethod", "exact")],
            ct);
        if (!reply.IsSuccess)
        {
            return WhmLookup.Unknown;
        }

        if (reply.Data is not { } data
            || data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("acct", out var accounts))
        {
            // WHM omits the list when nothing matched.
            return reply.Data is { ValueKind: JsonValueKind.Object } ? WhmLookup.NotFound : WhmLookup.Unknown;
        }

        if (accounts.ValueKind != JsonValueKind.Array)
        {
            return WhmLookup.Unknown;
        }

        foreach (var account in accounts.EnumerateArray())
        {
            if (!string.Equals(ReadString(account, "user"), user, StringComparison.Ordinal))
            {
                continue;
            }

            return WhmLookup.Found(new WhmAccount(
                user,
                ReadString(account, "domain") ?? string.Empty,
                ReadFlag(account, "suspended"),
                ReadString(account, "plan")));
        }

        return WhmLookup.NotFound;
    }

    /// <summary>Creates a cPanel account with <c>createacct</c>.</summary>
    /// <param name="connection">The server to create it on.</param>
    /// <param name="user">The account's username.</param>
    /// <param name="domain">The account's primary domain.</param>
    /// <param name="password">The account's password; never logged.</param>
    /// <param name="plan">The WHM package to create it on.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reply.</returns>
    public Task<WhmReply> CreateAccountAsync(WhmConnection connection, string user, string domain, string password, string plan, CancellationToken ct) =>
        CallAsync(
            connection,
            "createacct",
            [new("username", user), new("domain", domain), new("password", password), new("plan", plan)],
            ct);

    /// <summary>Suspends an account with <c>suspendacct</c>.</summary>
    /// <param name="connection">The server the account is on.</param>
    /// <param name="user">The account's username.</param>
    /// <param name="reason">The reason WHM records and shows the account owner; left out of the call when blank, since WHM takes it as optional.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reply.</returns>
    public Task<WhmReply> SuspendAccountAsync(WhmConnection connection, string user, string reason, CancellationToken ct) =>
        CallAsync(
            connection,
            "suspendacct",
            string.IsNullOrWhiteSpace(reason) ? [new("user", user)] : [new("user", user), new("reason", reason)],
            ct);

    /// <summary>Unsuspends an account with <c>unsuspendacct</c>.</summary>
    /// <param name="connection">The server the account is on.</param>
    /// <param name="user">The account's username.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reply.</returns>
    public Task<WhmReply> UnsuspendAccountAsync(WhmConnection connection, string user, CancellationToken ct) =>
        CallAsync(connection, "unsuspendacct", [new("user", user)], ct);

    /// <summary>Removes an account with <c>removeacct</c>, which takes <c>username</c>, not <c>user</c>.</summary>
    /// <param name="connection">The server the account is on.</param>
    /// <param name="user">The account's username.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reply.</returns>
    public Task<WhmReply> RemoveAccountAsync(WhmConnection connection, string user, CancellationToken ct) =>
        CallAsync(connection, "removeacct", [new("username", user)], ct);

    /// <summary>Moves an account onto another package with <c>changepackage</c>.</summary>
    /// <param name="connection">The server the account is on.</param>
    /// <param name="user">The account's username.</param>
    /// <param name="package">The package name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reply.</returns>
    public Task<WhmReply> ChangePackageAsync(WhmConnection connection, string user, string package, CancellationToken ct) =>
        CallAsync(connection, "changepackage", [new("user", user), new("pkg", package)], ct);

    /// <summary>Changes an account's password with <c>passwd</c>.</summary>
    /// <param name="connection">The server the account is on.</param>
    /// <param name="user">The account's username.</param>
    /// <param name="password">The new password; never logged.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reply.</returns>
    public Task<WhmReply> ChangePasswordAsync(WhmConnection connection, string user, string password, CancellationToken ct) =>
        CallAsync(connection, "passwd", [new("user", user), new("password", password)], ct);

    /// <summary>Opens a cPanel session for an account with <c>create_user_session</c>.</summary>
    /// <param name="connection">The server the account is on.</param>
    /// <param name="user">The account's username.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The one-time login URL, or null when WHM refused or answered without one.</returns>
    public async Task<string?> CreateUserSessionAsync(WhmConnection connection, string user, CancellationToken ct)
    {
        var reply = await CallAsync(connection, "create_user_session", [new("user", user), new("service", "cpaneld")], ct);
        return reply.IsSuccess ? ReadString(reply.Data, "url") : null;
    }

    /// <summary>Calls one WHM API 1 function and reads its reply.</summary>
    /// <param name="connection">The server to call.</param>
    /// <param name="function">The function name, e.g. <c>createacct</c>; the only thing ever logged.</param>
    /// <param name="arguments">The function's arguments, sent in the form encoded request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reply; <see cref="WhmReply.Unreadable"/> on a transport failure or a body that is not a reply.</returns>
    private async Task<WhmReply> CallAsync(WhmConnection connection, string function, KeyValuePair<string, string>[] arguments, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);

        // The one place every call passes, so no operation can reach WHM with an empty value: a blank
        // search would match every account, and a blank user would name none.
        if (arguments.Any(a => string.IsNullOrWhiteSpace(a.Value)))
        {
            _logger.LogWarning("WHM {Function} was not sent: an argument was empty.", function);
            return WhmReply.Unreadable;
        }

        WhmReply reply;
        try
        {
            // Built inside the try: a host name that is not a valid URI throws here, and it must
            // end as an unreadable reply like any other failure to reach the server.
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{connection.BaseUrl.TrimEnd('/')}{ApiPath}{function}?api.version=1")
            {
                Content = new FormUrlEncodedContent(arguments),
            };
            request.Headers.TryAddWithoutValidation("Authorization", $"{AuthScheme} {connection.Username}:{connection.ApiToken}");

            // Headers first, so the body is not buffered before its size is known.
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("WHM {Function} answered HTTP {StatusCode}.", function, (int)response.StatusCode);
                return WhmReply.Unreadable;
            }

            // Throws HttpRequestException past the cap, from the declared length or while reading,
            // which the handler below reports as an unreadable reply.
            await response.Content.LoadIntoBufferAsync(MaxResponseBytes, ct);

            // Parsed from the buffered stream, inside the response's lifetime, so the body is not
            // copied a second time into an array of its own.
            await using var body = await response.Content.ReadAsStreamAsync(ct);
            reply = await ParseAsync(body, ct);
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException or TimeoutRejectedException or UriFormatException) && !ct.IsCancellationRequested)
        {
            // The exception type only: an exception's text could name the request.
            _logger.LogWarning("WHM {Function} could not be sent ({ErrorType}).", function, ex.GetType().Name);
            return WhmReply.Unreadable;
        }

        if (reply.IsRefusal)
        {
            _logger.LogWarning("WHM {Function} was refused.", function);
        }
        else if (!reply.IsSuccess)
        {
            _logger.LogWarning("WHM {Function} answered a body that is not a WHM reply.", function);
        }

        return reply;
    }

    /// <summary>Reads a WHM API 1 body.</summary>
    /// <param name="body">The response body, UTF-8 JSON.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with its data, a refusal, or unreadable.</returns>
    private static async Task<WhmReply> ParseAsync(Stream body, CancellationToken ct)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: ct);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("metadata", out var metadata)
                || metadata.ValueKind != JsonValueKind.Object
                || !metadata.TryGetProperty("result", out var result)
                || result.ValueKind != JsonValueKind.Number
                || !result.TryGetInt32(out var code))
            {
                return WhmReply.Unreadable;
            }

            if (code != 1)
            {
                return code == 0 ? WhmReply.Refused : WhmReply.Unreadable;
            }

            JsonElement? data = root.TryGetProperty("data", out var found) ? found.Clone() : null;
            return new WhmReply(true, false, data);
        }
        catch (JsonException)
        {
            return WhmReply.Unreadable;
        }
    }

    /// <summary>Reads a string property, accepting a number as its text.</summary>
    /// <param name="element">The object to read from, or null.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The value, or null when absent or of another kind.</returns>
    private static string? ReadString(JsonElement? element, string name)
    {
        if (element is not { ValueKind: JsonValueKind.Object } value || !value.TryGetProperty(name, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null,
        };
    }

    /// <summary>Reads a WHM 0/1 flag, which WHM sends as a number or as its text.</summary>
    /// <param name="element">The object to read from.</param>
    /// <param name="name">The property name.</param>
    /// <returns>True when the flag is 1 or true.</returns>
    private static bool ReadFlag(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => property.TryGetInt32(out var number) && number != 0,
            JsonValueKind.String => int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var text) && text != 0,
            _ => false,
        };
    }
}
