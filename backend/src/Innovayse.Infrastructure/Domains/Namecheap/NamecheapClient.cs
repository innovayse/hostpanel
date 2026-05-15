namespace Innovayse.Infrastructure.Domains.Namecheap;

using System.Xml.Linq;
using Microsoft.Extensions.Options;

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper for the Namecheap XML API v2.
/// Handles authentication parameter injection, request dispatch, and error mapping.
/// </summary>
public sealed class NamecheapClient
{
    /// <summary>The underlying HTTP client used to call the Namecheap API.</summary>
    private readonly HttpClient _http;

    /// <summary>Resolved Namecheap configuration settings.</summary>
    private readonly NamecheapSettings _settings;

    /// <summary>
    /// Initializes a new instance of <see cref="NamecheapClient"/>.
    /// </summary>
    /// <param name="http">The <see cref="HttpClient"/> configured by <c>IHttpClientFactory</c>.</param>
    /// <param name="options">Bound <see cref="NamecheapSettings"/> options.</param>
    public NamecheapClient(HttpClient http, IOptions<NamecheapSettings> options)
    {
        _http = http;
        _settings = options.Value;
    }

    /// <summary>Gets whether the Namecheap API is configured with valid credentials.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.ApiKey) &&
        !string.IsNullOrWhiteSpace(_settings.ApiUrl) &&
        !string.IsNullOrWhiteSpace(_settings.ApiUser);
    /// <summary>
    /// Executes a Namecheap XML API command and returns the parsed response document.
    /// </summary>
    /// <param name="command">The API command name (e.g. namecheap.domains.create).</param>
    /// <param name="parameters">Additional command-specific parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed XDocument of the successful API response.</returns>
    /// <exception cref="RegistrarException">Thrown when the API response contains ERROR status.</exception>
    public async Task<XDocument> ExecuteAsync(
        string command,
        Dictionary<string, string> parameters,
        CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return XDocument.Parse("<ApiResponse Status=\"OK\"><CommandResponse /></ApiResponse>");
        }

        parameters["ApiUser"] = _settings.ApiUser;
        parameters["ApiKey"] = _settings.ApiKey;
        parameters["UserName"] = _settings.ApiUser;
        parameters["ClientIp"] = _settings.ClientIp;
        parameters["Command"] = command;

        var queryString = string.Join("&",
            parameters.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        var url = $"{_settings.ApiUrl.TrimEnd('/')}?{queryString}";

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync(ct);
        var doc = XDocument.Parse(xml);

        var apiResponse = doc.Root;
        var status = apiResponse?.Attribute("Status")?.Value;

        if (string.Equals(status, "ERROR", StringComparison.OrdinalIgnoreCase))
        {
            var errorEl = apiResponse
                ?.Element(XName.Get("Errors", apiResponse.Name.NamespaceName))
                ?.Element(XName.Get("Error", apiResponse.Name.NamespaceName));

            var errorCode = errorEl?.Attribute("Number")?.Value ?? "0";
            var errorMessage = errorEl?.Value ?? "Unknown registrar error";

            throw RegistrarException.FromCode(errorCode, errorMessage);
        }

        return doc;
    }
}
