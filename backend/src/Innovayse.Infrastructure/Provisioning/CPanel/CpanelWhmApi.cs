namespace Innovayse.Infrastructure.Provisioning.CPanel;

using System.Text.Json;

/// <summary>
/// Lightweight per-server WHM API helper.
/// Unlike <see cref="CPanelClient"/> (which is DI-configured for a single server),
/// this class accepts per-call connection details and is used for multi-server operations
/// such as disk usage reporting.
/// </summary>
public sealed class CpanelWhmApi
{
    private readonly HttpClient _http;

    private CpanelWhmApi(HttpClient http) => _http = http;

    /// <summary>Creates an instance connected to the given server using an API token.</summary>
    public static CpanelWhmApi Create(string hostname, string username, string apiToken, IHttpClientFactory factory)
    {
        var http = factory.CreateClient();
        http.BaseAddress = new Uri($"https://{hostname}:2087");
        http.DefaultRequestHeaders.Add("Authorization", $"whm {username}:{apiToken}");
        http.Timeout = TimeSpan.FromSeconds(30);
        return new CpanelWhmApi(http);
    }

    /// <summary>Creates an instance connected to the given server using an access hash.</summary>
    public static CpanelWhmApi CreateWithHash(string hostname, string username, string accessHash, IHttpClientFactory factory)
    {
        var http = factory.CreateClient();
        http.BaseAddress = new Uri($"https://{hostname}:2087");
        // WHM access hash auth — strip whitespace from hash
        var cleanHash = accessHash.Replace("\n", "").Replace("\r", "").Trim();
        http.DefaultRequestHeaders.Add("Authorization", $"WHM {username}:{cleanHash}");
        http.Timeout = TimeSpan.FromSeconds(30);
        return new CpanelWhmApi(http);
    }

    /// <summary>
    /// Calls WHM <c>listaccts</c> and returns disk/bandwidth usage for all accounts on the server.
    /// </summary>
    public async Task<List<WhmAccountUsage>> ListAccountsAsync(CancellationToken ct)
    {
        using var response = await _http.GetAsync("/json-api/listaccts?api.version=1", ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        var results = new List<WhmAccountUsage>();

        if (!doc.RootElement.TryGetProperty("data", out var data)) return results;
        if (!data.TryGetProperty("acct", out var accts)) return results;

        foreach (var acct in accts.EnumerateArray())
        {
            var user   = acct.TryGetProperty("user",   out var u) ? u.GetString() ?? "" : "";
            var domain = acct.TryGetProperty("domain", out var d) ? d.GetString() ?? "" : "";
            var owner  = acct.TryGetProperty("owner",  out var o) ? o.GetString() ?? "" : "";

            // Disk usage: "diskused" in MB, "disklimit" in MB ("unlimited" = 0)
            long diskUsed  = ParseMb(acct, "diskused");
            long diskLimit = ParseLimitMb(acct, "disklimit");

            // Bandwidth: "bwused" in MB, "bwlimit" in MB
            long bwUsed  = ParseMb(acct, "bwused");
            long bwLimit = ParseLimitMb(acct, "bwlimit");

            results.Add(new WhmAccountUsage(user, domain, owner, diskUsed, diskLimit, bwUsed, bwLimit));
        }

        return results;
    }

    private static long ParseMb(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return 0;
        if (val.ValueKind == JsonValueKind.Number) return (long)val.GetDouble();
        if (val.ValueKind == JsonValueKind.String)
        {
            var s = val.GetString() ?? "0";
            return double.TryParse(s, out var d) ? (long)d : 0;
        }
        return 0;
    }

    private static long ParseLimitMb(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return 0;
        if (val.ValueKind == JsonValueKind.String)
        {
            var s = val.GetString() ?? "";
            if (s.Equals("unlimited", StringComparison.OrdinalIgnoreCase)) return 0;
            return double.TryParse(s, out var d) ? (long)d : 0;
        }
        if (val.ValueKind == JsonValueKind.Number) return (long)val.GetDouble();
        return 0;
    }
}

/// <summary>Disk and bandwidth usage for a single WHM account.</summary>
public sealed record WhmAccountUsage(
    string Username,
    string Domain,
    string Owner,
    long DiskUsedMb,
    long DiskLimitMb,
    long BwUsedMb,
    long BwLimitMb);
