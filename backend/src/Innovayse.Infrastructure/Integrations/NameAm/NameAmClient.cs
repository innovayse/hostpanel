namespace Innovayse.Infrastructure.Integrations.NameAm;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Innovayse.Domain.Settings.Interfaces;
using Innovayse.Infrastructure.Integrations.NameAm.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper for the Name.am JSON REST API.
/// Handles JWT-based authentication, automatic token refresh on 401, and request dispatch.
/// </summary>
public sealed class NameAmClient
{
    /// <summary>The underlying HTTP client used to call the Name.am API.</summary>
    private readonly HttpClient _http;

    /// <summary>Resolved Name.am configuration settings.</summary>
    private readonly NameAmOptions _settings;

    /// <summary>Setting repository for reading integration overrides from the database.</summary>
    private readonly ISettingRepository _settingRepo;

    /// <summary>Logger for structured diagnostics.</summary>
    private readonly ILogger<NameAmClient> _logger;

    /// <summary>Cached JWT access token obtained from <c>/auth/login</c>.</summary>
    private string? _accessToken;

    /// <summary>
    /// Effective configuration: values saved on the admin Integrations page
    /// (<c>integration:nameam:*</c>) over the bound <see cref="NameAmOptions"/>.
    /// <see langword="null"/> until <see cref="LoadOverridesAsync"/> has run.
    /// </summary>
    private NameAmOptions? _effective;

    /// <summary>Semaphore guarding concurrent login attempts to prevent token races.</summary>
    private readonly SemaphoreSlim _loginLock = new(1, 1);

    /// <summary>Shared JSON serializer options with camelCase naming.</summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Initializes a new instance of <see cref="NameAmClient"/>.
    /// </summary>
    /// <param name="http">The <see cref="HttpClient"/> configured by <c>IHttpClientFactory</c>.</param>
    /// <param name="options">Bound <see cref="NameAmOptions"/> options.</param>
    /// <param name="settingRepo">Setting repository for reading DB-stored integration config.</param>
    /// <param name="logger">Logger for structured diagnostics.</param>
    public NameAmClient(
        HttpClient http,
        IOptions<NameAmOptions> options,
        ISettingRepository settingRepo,
        ILogger<NameAmClient> logger)
    {
        _http = http;
        _settings = options.Value;
        _settingRepo = settingRepo;
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
    public async Task VerifyCredentialsAsync(CancellationToken ct)
    {
        await LoadOverridesAsync(ct);
        _accessToken = null;
        await LoginAsync(ct);
    }

    /// <summary>
    /// Sends a GET request to the Name.am API and returns the parsed JSON response.
    /// Automatically authenticates and retries once on 401.
    /// </summary>
    /// <param name="path">Relative API path (e.g. <c>/client/domains</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed <see cref="JsonDocument"/> of the response body.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API returns a non-success status after retry.</exception>
    public async Task<JsonDocument> GetAsync(string path, CancellationToken ct)
    {
        await LoadOverridesAsync(ct);
        await EnsureAuthenticatedAsync(ct);

        var url = BuildUrl(path);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        SetAuthHeader(request);

        using var response = await _http.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("Name.am API returned 401 for GET {Path}, re-authenticating", path);
            await LoginAsync(ct);

            using var retryRequest = new HttpRequestMessage(HttpMethod.Get, url);
            SetAuthHeader(retryRequest);

            using var retryResponse = await _http.SendAsync(retryRequest, ct);
            retryResponse.EnsureSuccessStatusCode();
            return await ParseResponseAsync(retryResponse, ct);
        }

        response.EnsureSuccessStatusCode();
        return await ParseResponseAsync(response, ct);
    }

    /// <summary>
    /// Sends a POST request with a JSON body to the Name.am API and returns the parsed JSON response.
    /// Automatically authenticates and retries once on 401.
    /// </summary>
    /// <param name="path">Relative API path (e.g. <c>/client/carts/purchase</c>).</param>
    /// <param name="body">Object to serialize as JSON in the request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed <see cref="JsonDocument"/> of the response body.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API returns a non-success status after retry.</exception>
    public async Task<JsonDocument> PostAsync(string path, object body, CancellationToken ct)
    {
        await LoadOverridesAsync(ct);
        await EnsureAuthenticatedAsync(ct);

        var url = BuildUrl(path);
        var json = JsonSerializer.Serialize(body, _jsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        SetAuthHeader(request);

        using var response = await _http.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("Name.am API returned 401 for POST {Path}, re-authenticating", path);
            await LoginAsync(ct);

            using var retryRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            SetAuthHeader(retryRequest);

            using var retryResponse = await _http.SendAsync(retryRequest, ct);
            retryResponse.EnsureSuccessStatusCode();
            return await ParseResponseAsync(retryResponse, ct);
        }

        response.EnsureSuccessStatusCode();
        return await ParseResponseAsync(response, ct);
    }

    /// <summary>
    /// Sends a PUT request with a JSON body to the Name.am API and returns the parsed JSON response.
    /// Automatically authenticates and retries once on 401.
    /// </summary>
    /// <param name="path">Relative API path (e.g. <c>/client/domains/example.am</c>).</param>
    /// <param name="body">Object to serialize as JSON in the request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed <see cref="JsonDocument"/> of the response body.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API returns a non-success status after retry.</exception>
    public async Task<JsonDocument> PutAsync(string path, object body, CancellationToken ct)
    {
        await LoadOverridesAsync(ct);
        await EnsureAuthenticatedAsync(ct);

        var url = BuildUrl(path);
        var json = JsonSerializer.Serialize(body, _jsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        SetAuthHeader(request);

        using var response = await _http.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("Name.am API returned 401 for PUT {Path}, re-authenticating", path);
            await LoginAsync(ct);

            using var retryRequest = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            SetAuthHeader(retryRequest);

            using var retryResponse = await _http.SendAsync(retryRequest, ct);
            retryResponse.EnsureSuccessStatusCode();
            return await ParseResponseAsync(retryResponse, ct);
        }

        response.EnsureSuccessStatusCode();
        return await ParseResponseAsync(response, ct);
    }

    /// <summary>
    /// Ensures the client has a valid access token, logging in if none is cached.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    private async Task EnsureAuthenticatedAsync(CancellationToken ct)
    {
        if (_accessToken is not null)
        {
            return;
        }

        await LoginAsync(ct);
    }

    /// <summary>
    /// Authenticates with the Name.am API via <c>POST /auth/login</c> and caches the JWT access token.
    /// Thread-safe via <see cref="_loginLock"/>.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="HttpRequestException">Thrown when authentication fails.</exception>
    private async Task LoginAsync(CancellationToken ct)
    {
        await _loginLock.WaitAsync(ct);
        try
        {
            _logger.LogInformation("Authenticating with Name.am API as {Email}", Settings.Email);

            var loginPayload = new
            {
                email = Settings.Email,
                password = Settings.Password,
                token = "",
            };

            var json = JsonSerializer.Serialize(loginPayload, _jsonOptions);
            var url = BuildUrl("/auth/login");

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };

            using var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            using var doc = await ParseResponseAsync(response, ct);

            var accessToken = doc.RootElement.GetProperty("accessToken").GetString();

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new HttpRequestException("Name.am login response did not contain an access token.");
            }

            // The API returns "Bearer eyJ..." — strip the "Bearer " prefix if present.
            _accessToken = accessToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? accessToken["Bearer ".Length..]
                : accessToken;

            _logger.LogInformation("Successfully authenticated with Name.am API");
        }
        finally
        {
            _loginLock.Release();
        }
    }

    /// <summary>
    /// Sets the <c>Authorization: Bearer</c> header on an outgoing request using the cached token.
    /// </summary>
    /// <param name="request">The HTTP request to decorate.</param>
    private void SetAuthHeader(HttpRequestMessage request)
    {
        if (_accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }
    }

    /// <summary>
    /// Gets the effective settings, or the bound options when <see cref="LoadOverridesAsync"/>
    /// has not run yet.
    /// </summary>
    private NameAmOptions Settings => _effective ?? _settings;

    /// <summary>
    /// Loads the Name.am fields saved on the admin Integrations page and lays every non-empty one
    /// over <see cref="NameAmOptions"/> from <c>appsettings.json</c>. Before this, the page saved
    /// e-mail, password and API URL that no request ever read -- only <c>test_mode</c> was honoured.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The effective settings.</returns>
    private async Task<NameAmOptions> LoadOverridesAsync(CancellationToken ct)
    {
        if (_effective is not null)
        {
            return _effective;
        }

        async Task<string?> ReadAsync(string field)
        {
            var setting = await _settingRepo.FindByKeyAsync($"integration:nameam:{field}", ct);
            return string.IsNullOrWhiteSpace(setting?.Value) ? null : setting.Value.Trim();
        }

        var testMode = await ReadAsync("test_mode");
        _effective = new NameAmOptions
        {
            Email = await ReadAsync("email") ?? _settings.Email,
            Password = await ReadAsync("password") ?? _settings.Password,
            ApiUrl = await ReadAsync("api_url") ?? _settings.ApiUrl,
            TestMode = testMode is null
                ? _settings.TestMode
                : string.Equals(testMode, "true", StringComparison.OrdinalIgnoreCase),
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
        var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }
}
