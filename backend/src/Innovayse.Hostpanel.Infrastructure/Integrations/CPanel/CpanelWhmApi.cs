using System.Globalization;
using System.Text.Json;
using Innovayse.Hostpanel.Infrastructure.Resilience;
using Innovayse.SDK.Plugins.HostingPanel;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.CPanel;

/// <summary>
/// Lightweight per-server WHM API helper.
/// Unlike <see cref="CPanelClient"/> (which is DI-configured for a single server),
/// this class accepts per-call connection details and is used for multi-server operations
/// such as disk usage reporting.
/// </summary>
public sealed class CpanelWhmApi
{
    /// <summary>The port WHM's JSON API listens on over TLS.</summary>
    private const int WhmPort = 2087;

    /// <summary>The HTTP client already pointed at one WHM server and carrying its credentials.</summary>
    private readonly HttpClient _http;

    /// <summary>Wraps a pre-configured client; instances come from <c>Create</c> and <c>CreateWithHash</c>.</summary>
    /// <param name="http">Client with base address, authorization header and timeout already set.</param>
    private CpanelWhmApi(HttpClient http) => _http = http;

    /// <summary>Creates an instance connected to the given server using an API token.</summary>
    /// <param name="hostname">WHM host name; port 2087 is used.</param>
    /// <param name="username">WHM user the token belongs to.</param>
    /// <param name="apiToken">WHM API token.</param>
    /// <param name="factory">Source of the underlying HTTP client.</param>
    /// <returns>A client bound to that server.</returns>
    public static CpanelWhmApi Create(string hostname, string username, string apiToken, IHttpClientFactory factory) =>
        Connect(hostname, $"whm {username}:{apiToken}", factory);

    /// <summary>Creates an instance connected to the given server using an access hash.</summary>
    /// <param name="hostname">WHM host name; port 2087 is used.</param>
    /// <param name="username">WHM user the hash belongs to.</param>
    /// <param name="accessHash">WHM access hash; whitespace is stripped.</param>
    /// <param name="factory">Source of the underlying HTTP client.</param>
    /// <returns>A client bound to that server.</returns>
    public static CpanelWhmApi CreateWithHash(string hostname, string username, string accessHash, IHttpClientFactory factory)
    {
        // WHM access hash auth: the hash is stored wrapped across lines, so whitespace is stripped.
        var cleanHash = accessHash.Replace("\n", "").Replace("\r", "").Trim();
        return Connect(hostname, $"WHM {username}:{cleanHash}", factory);
    }

    /// <summary>
    /// Calls WHM <c>listaccts</c> and returns disk/bandwidth usage for all accounts on the server.
    /// </summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Usage per account on the server.</returns>
    public async Task<List<WhmAccountUsage>> ListAccountsAsync(CancellationToken ct)
    {
        using var response = await _http.GetAsync("/json-api/listaccts?api.version=1", ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        var results = new List<WhmAccountUsage>();

        if (!doc.RootElement.TryGetProperty("data", out var data))
        {
            return results;
        }

        if (!data.TryGetProperty("acct", out var accts))
        {
            return results;
        }

        foreach (var acct in accts.EnumerateArray())
        {
            var user = acct.TryGetProperty("user", out var u) ? u.GetString() ?? "" : "";
            var domain = acct.TryGetProperty("domain", out var d) ? d.GetString() ?? "" : "";
            var owner = acct.TryGetProperty("owner", out var o) ? o.GetString() ?? "" : "";

            // Disk usage: "diskused" in MB, "disklimit" in MB ("unlimited" = 0)
            long diskUsed = ParseMb(acct, "diskused");
            long diskLimit = ParseMb(acct, "disklimit");

            // Bandwidth: "bwused" in MB, "bwlimit" in MB
            long bwUsed = ParseMb(acct, "bwused");
            long bwLimit = ParseMb(acct, "bwlimit");

            results.Add(new WhmAccountUsage(user, domain, owner, diskUsed, diskLimit, bwUsed, bwLimit));
        }

        return results;
    }

    /// <summary>Builds a client for one WHM server with the given authorization header value.</summary>
    /// <param name="hostname">WHM host name; port 2087 is used.</param>
    /// <param name="authorization">The complete <c>Authorization</c> header value.</param>
    /// <param name="factory">Source of the underlying HTTP client.</param>
    /// <returns>A client bound to that server.</returns>
    private static CpanelWhmApi Connect(string hostname, string authorization, IHttpClientFactory factory)
    {
        var http = factory.CreateClient();
        http.BaseAddress = new Uri(HostingPanelAddress.BaseUrl(hostname, WhmPort));
        http.DefaultRequestHeaders.Add("Authorization", authorization);
        http.Timeout = HttpBackstopTimeouts.Whm;
        return new CpanelWhmApi(http);
    }

    /// <summary>
    /// Reads a megabyte figure that WHM sends either as a number or as a numeric string, where
    /// the string <c>unlimited</c> (a limit with no cap) reads as 0.
    /// </summary>
    /// <param name="el">The JSON object holding the figure.</param>
    /// <param name="prop">Name of the property to read.</param>
    /// <returns>Whole megabytes; 0 when the property is missing, unlimited or not numeric.</returns>
    private static long ParseMb(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val))
        {
            return 0;
        }

        if (val.ValueKind == JsonValueKind.Number)
        {
            return (long)val.GetDouble();
        }

        if (val.ValueKind == JsonValueKind.String)
        {
            // Invariant: WHM writes "1.5" with a dot whatever the culture of this process is.
            return double.TryParse(val.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                ? (long)d
                : 0;
        }

        return 0;
    }
}
