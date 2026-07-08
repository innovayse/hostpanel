namespace Innovayse.Infrastructure.Email.Mailcow;

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper for the Mailcow REST API.
/// Handles API-key authentication and request dispatch.
/// Consumed exclusively by <see cref="MailcowMailServerClient"/>.
/// </summary>
public sealed class MailcowClient(
    HttpClient http,
    IOptions<MailcowSettings> options,
    ILogger<MailcowClient> logger)
{
    private readonly MailcowSettings _settings = options.Value;

    /// <summary>Shared JSON serializer options with case-insensitive property matching.</summary>
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Sends a POST request to the Mailcow API and returns the parsed JSON response.
    /// </summary>
    /// <param name="path">Relative API path (e.g. <c>/api/v1/add/domain</c>).</param>
    /// <param name="body">Object to serialize as the JSON request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed <see cref="JsonElement"/> of the response body.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API returns a non-success status.</exception>
    public async Task<JsonElement> PostAsync(string path, object body, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.ApiUrl}{path}");
        request.Headers.Add("X-API-Key", _settings.ApiKey);
        request.Content = JsonContent.Create(body);

        var response = await http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        logger.LogDebug("Mailcow POST {Path} → {Status}: {Body}", path, response.StatusCode, json);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Mailcow API error {response.StatusCode}: {json}");

        return JsonSerializer.Deserialize<JsonElement>(json, JsonOpts);
    }

    /// <summary>
    /// Sends a GET request to the Mailcow API and returns the parsed JSON response.
    /// </summary>
    /// <param name="path">Relative API path (e.g. <c>/api/v1/get/dkim/{domain}</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed <see cref="JsonElement"/> of the response body.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API returns a non-success status.</exception>
    public async Task<JsonElement> GetAsync(string path, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_settings.ApiUrl}{path}");
        request.Headers.Add("X-API-Key", _settings.ApiKey);

        var response = await http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        logger.LogDebug("Mailcow GET {Path} → {Status}", path, response.StatusCode);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Mailcow API error {response.StatusCode}: {json}");

        return JsonSerializer.Deserialize<JsonElement>(json, JsonOpts);
    }
}
