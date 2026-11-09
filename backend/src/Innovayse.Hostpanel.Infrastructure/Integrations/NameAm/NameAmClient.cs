using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Integrations.NameAm.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.NameAm;

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper for the Name.am JSON REST API.
/// Handles JWT-based authentication through the shared <see cref="NameAmTokenStore"/>, token refresh on 401, and request dispatch.
/// </summary>
public sealed class NameAmClient
{
    /// <summary>The underlying HTTP client used to call the Name.am API.</summary>
    private readonly HttpClient _http;

    /// <summary>Resolved Name.am configuration settings.</summary>
    private readonly NameAmOptions _settings;

    /// <summary>The data context, for reading integration overrides from the database.</summary>
    private readonly AppDbContext _db;

    /// <summary>Decrypts the stored account password.</summary>
    private readonly ISecretSettingCipher _cipher;

    /// <summary>Logger for structured diagnostics.</summary>
    private readonly ILogger<NameAmClient> _logger;

    /// <summary>The process-wide holder of the JWT access token obtained from <c>/auth/login</c>.</summary>
    private readonly NameAmTokenStore _tokens;

    /// <summary>
    /// Effective configuration: values saved on the admin Integrations page
    /// (<c>integration:nameam:*</c>) over the bound <see cref="NameAmOptions"/>.
    /// <see langword="null"/> until <see cref="LoadOverridesAsync"/> has run.
    /// </summary>
    private NameAmOptions? _effective;

    /// <summary>Shared JSON serializer options with camelCase naming.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Initializes a new instance of <see cref="NameAmClient"/>.
    /// </summary>
    /// <param name="http">The <see cref="HttpClient"/> configured by <c>IHttpClientFactory</c>.</param>
    /// <param name="options">Bound <see cref="NameAmOptions"/> options.</param>
    /// <param name="db">The data context, for reading DB-stored integration config.</param>
    /// <param name="cipher">Decrypts the stored account password.</param>
    /// <param name="tokens">The process-wide holder of the access token.</param>
    /// <param name="logger">Logger for structured diagnostics.</param>
    public NameAmClient(
        HttpClient http,
        IOptions<NameAmOptions> options,
        AppDbContext db,
        ISecretSettingCipher cipher,
        NameAmTokenStore tokens,
        ILogger<NameAmClient> logger)
    {
        _http = http;
        _settings = options.Value;
        _db = db;
        _cipher = cipher;
        _tokens = tokens;
        _logger = logger;
    }

    /// <summary>
    /// Gets whether the Name.am API has credentials, from the admin settings or from configuration.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when both an e-mail and a password are available.</returns>
    public async Task<bool> IsConfiguredAsync(CancellationToken ct)
    {
        var effective = await LoadOverridesAsync(ct);
        return !string.IsNullOrWhiteSpace(effective.Email) &&
            !string.IsNullOrWhiteSpace(effective.Password);
    }

    /// <summary>
    /// Authenticates against Name.am with the effective credentials, bypassing the cached token,
    /// so the admin "Test Connection" reflects what Name.am actually accepts.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="HttpRequestException">Thrown when Name.am rejects the login or is unreachable.</exception>
    /// <returns>A task that completes when Name.am has accepted the credentials.</returns>
    public async Task VerifyCredentialsAsync(CancellationToken ct)
    {
        await LoadOverridesAsync(ct);
        await _tokens.ReplaceAsync(ConnectionKey, LoginAsync, ct);
    }

    /// <summary>
    /// Sends a GET request to the Name.am API and returns the parsed JSON response.
    /// Authenticates with the shared token and retries once on 401.
    /// </summary>
    /// <param name="path">Relative API path (e.g. <c>/client/domains</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed <see cref="JsonDocument"/> of the response body.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API returns a non-success status after retry.</exception>
    /// <exception cref="JsonException">Thrown when the answer is not JSON.</exception>
    public Task<JsonDocument> GetAsync(string path, CancellationToken ct)
    {
        return SendAsync(HttpMethod.Get, path, null, ct);
    }

    /// <summary>
    /// Sends a POST request with a JSON body to the Name.am API and returns the parsed JSON response.
    /// Authenticates with the shared token and retries once on 401.
    /// </summary>
    /// <typeparam name="TBody">The wire shape of the body.</typeparam>
    /// <param name="path">Relative API path (e.g. <c>/client/carts/purchase</c>).</param>
    /// <param name="body">The body, serialised as camelCase JSON with absent members left out.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed <see cref="JsonDocument"/> of the response body.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API returns a non-success status after retry.</exception>
    /// <exception cref="JsonException">Thrown when the answer is not JSON.</exception>
    public Task<JsonDocument> PostAsync<TBody>(string path, TBody body, CancellationToken ct)
    {
        return SendAsync(HttpMethod.Post, path, JsonSerializer.Serialize(body, JsonOptions), ct);
    }

    /// <summary>
    /// Sends a PUT request with a JSON body to the Name.am API and returns the parsed JSON response.
    /// Authenticates with the shared token and retries once on 401.
    /// </summary>
    /// <typeparam name="TBody">The wire shape of the body.</typeparam>
    /// <param name="path">Relative API path (e.g. <c>/client/domains/example.am</c>).</param>
    /// <param name="body">The body, serialised as camelCase JSON with absent members left out.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed <see cref="JsonDocument"/> of the response body.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API returns a non-success status after retry.</exception>
    /// <exception cref="JsonException">Thrown when the answer is not JSON.</exception>
    public Task<JsonDocument> PutAsync<TBody>(string path, TBody body, CancellationToken ct)
    {
        return SendAsync(HttpMethod.Put, path, JsonSerializer.Serialize(body, JsonOptions), ct);
    }

    /// <summary>
    /// Sends one request with the shared token, and once more with a fresh token when Name.am
    /// answers 401.
    /// </summary>
    /// <remarks>
    /// Repeating after a 401 is safe for the purchase too: Name.am rejects an expired or revoked
    /// token before it looks at the request, so the first attempt did nothing and the repeat is the
    /// only attempt that is processed. Any other failure is never repeated here.
    /// </remarks>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">Relative API path.</param>
    /// <param name="json">The serialised body, or <see langword="null"/> for a request without one.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed <see cref="JsonDocument"/> of the response body.</returns>
    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, string? json, CancellationToken ct)
    {
        await LoadOverridesAsync(ct);

        var url = BuildUrl(path);
        var token = await _tokens.GetAsync(ConnectionKey, LoginAsync, ct);

        using var response = await SendOnceAsync(method, url, json, token, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            response.EnsureSuccessStatusCode();
            return await ParseResponseAsync(response, ct);
        }

        _logger.LogWarning("Name.am API returned 401 for {Method} {Path}, re-authenticating", method, path);
        token = await _tokens.RefreshAsync(ConnectionKey, token, LoginAsync, ct);

        using var retryResponse = await SendOnceAsync(method, url, json, token, ct);
        retryResponse.EnsureSuccessStatusCode();
        return await ParseResponseAsync(retryResponse, ct);
    }

    /// <summary>
    /// Sends a request once, carrying the access token.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="url">The absolute URL.</param>
    /// <param name="json">The serialised body, or <see langword="null"/> for a request without one.</param>
    /// <param name="token">The access token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The response.</returns>
    private async Task<HttpResponseMessage> SendOnceAsync(
        HttpMethod method, string url, string? json, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, System.Net.Mime.MediaTypeNames.Application.Json);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);
        return await _http.SendAsync(request, ct);
    }

    /// <summary>
    /// Authenticates with the Name.am API via <c>POST /auth/login</c>. Called only through
    /// <see cref="NameAmTokenStore"/>, which holds its gate for the duration.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The access token, without the <c>Bearer</c> prefix.</returns>
    /// <exception cref="HttpRequestException">Thrown when authentication fails.</exception>
    private async Task<string> LoginAsync(CancellationToken ct)
    {
        // The account e-mail is personal data and stays out of the log.
        _logger.LogInformation("Authenticating with the Name.am API");

        var json = JsonSerializer.Serialize(new NameAmLoginWire(Settings.Email, Settings.Password, string.Empty), JsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl("/auth/login"))
        {
            Content = new StringContent(json, Encoding.UTF8, System.Net.Mime.MediaTypeNames.Application.Json),
        };

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var doc = await ParseResponseAsync(response, ct);

        var accessToken = doc.RootElement.TryGetProperty("accessToken", out var tokenEl) && tokenEl.ValueKind == JsonValueKind.String
            ? tokenEl.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new HttpRequestException("Name.am login response did not contain an access token.");
        }

        _logger.LogInformation("Authenticated with the Name.am API");

        // The API returns "Bearer <token>": strip the "Bearer " prefix if present.
        return accessToken.StartsWith(BearerScheme.HeaderPrefix, StringComparison.OrdinalIgnoreCase)
            ? accessToken[BearerScheme.HeaderPrefix.Length..]
            : accessToken;
    }

    /// <summary>
    /// Gets the key the shared token is cached under: the API address, the account and the test
    /// mode flag, so a token is only ever reused for the connection it was issued for.
    /// </summary>
    private string ConnectionKey => $"{Settings.ApiUrl.TrimEnd('/')}|{Settings.Email}|{Settings.TestMode}";

    /// <summary>
    /// Gets the effective settings, or the bound options when <see cref="LoadOverridesAsync"/>
    /// has not run yet.
    /// </summary>
    private NameAmOptions Settings => _effective ?? _settings;

    /// <summary>
    /// Loads the Name.am fields saved on the admin Integrations page and lays every non-empty one
    /// over <see cref="NameAmOptions"/> from <c>appsettings.json</c>, so every field the page saves
    /// (e-mail, password, API URL and <c>test_mode</c>) is honoured.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The effective settings.</returns>
    private async Task<NameAmOptions> LoadOverridesAsync(CancellationToken ct)
    {
        if (_effective is not null)
        {
            return _effective;
        }

        var emailKey = IntegrationSettingKeys.FieldKey(IntegrationSlugs.NameAm, IntegrationSettingKeys.EmailField);
        var passwordKey = IntegrationSettingKeys.FieldKey(IntegrationSlugs.NameAm, IntegrationSettingKeys.PasswordField);
        var apiUrlKey = IntegrationSettingKeys.FieldKey(IntegrationSlugs.NameAm, IntegrationSettingKeys.ApiUrlField);
        var testModeKey = IntegrationSettingKeys.FieldKey(IntegrationSlugs.NameAm, IntegrationSettingKeys.TestModeField);
        string[] keys = [emailKey, passwordKey, apiUrlKey, testModeKey];

        // One read for all four fields; read-only, so no tracking is needed.
        var stored = await _db.Settings
            .AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        // The password is a secret setting, stored encrypted; the other fields pass through unchanged.
        string? Read(string key) =>
            stored.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? _cipher.Reveal(value).Trim() : null;

        var testMode = Read(testModeKey);
        _effective = new NameAmOptions
        {
            Email = Read(emailKey) ?? _settings.Email,
            Password = Read(passwordKey) ?? _settings.Password,
            ApiUrl = Read(apiUrlKey) ?? _settings.ApiUrl,
            TestMode = testMode is null
                ? _settings.TestMode
                : string.Equals(testMode, IntegrationSettingKeys.EnabledValue, StringComparison.OrdinalIgnoreCase),
        };

        return _effective;
    }

    /// <summary>
    /// Gets whether test mode is active. The admin setting wins over
    /// <see cref="NameAmOptions.TestMode"/>; call <see cref="LoadOverridesAsync"/> first.
    /// </summary>
    public bool IsTestMode => Settings.TestMode;

    /// <summary>
    /// Builds a full URL by combining the configured API base URL with the given path.
    /// Appends <c>testmode=1</c> query parameter when test mode is enabled
    /// (either via DB setting or <c>appsettings.json</c>).
    /// </summary>
    /// <param name="path">Relative API path.</param>
    /// <returns>Absolute URL string.</returns>
    private string BuildUrl(string path)
    {
        var url = $"{Settings.ApiUrl.TrimEnd('/')}{path}";

        if (IsTestMode)
        {
            url += url.Contains('?') ? "&testmode=1" : "?testmode=1";
        }

        return url;
    }

    /// <summary>
    /// Reads and parses the response body as a <see cref="JsonDocument"/>.
    /// </summary>
    /// <param name="response">The HTTP response to parse.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed JSON document.</returns>
    private static async Task<JsonDocument> ParseResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }
}
