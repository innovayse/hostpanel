namespace Innovayse.Infrastructure.Domains.NameAm;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
    private readonly NameAmSettings _settings;

    /// <summary>Logger for structured diagnostics.</summary>
    private readonly ILogger<NameAmClient> _logger;

    /// <summary>Cached JWT access token obtained from <c>/auth/login</c>.</summary>
    private string? _accessToken;

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
    /// <param name="options">Bound <see cref="NameAmSettings"/> options.</param>
    /// <param name="logger">Logger for structured diagnostics.</param>
    public NameAmClient(HttpClient http, IOptions<NameAmSettings> options, ILogger<NameAmClient> logger)
    {
        _http = http;
        _settings = options.Value;
        _logger = logger;
    }

    /// <summary>Gets whether the Name.am API is configured with valid credentials.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.Email) &&
        !string.IsNullOrWhiteSpace(_settings.Password);

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
            _logger.LogInformation("Authenticating with Name.am API as {Email}", _settings.Email);

            var loginPayload = new
            {
                email = _settings.Email,
                password = _settings.Password,
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
    /// Builds a full URL by combining the configured API base URL with the given path.
    /// </summary>
    /// <param name="path">Relative API path.</param>
    /// <returns>Absolute URL string.</returns>
    private string BuildUrl(string path)
    {
        return $"{_settings.ApiUrl.TrimEnd('/')}{path}";
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
