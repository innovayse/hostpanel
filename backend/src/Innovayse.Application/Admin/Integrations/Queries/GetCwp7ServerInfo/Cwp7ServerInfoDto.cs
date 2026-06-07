namespace Innovayse.Application.Admin.Integrations.Queries.GetCwp7ServerInfo;

/// <summary>Live server status returned by the CWP7 server-info endpoint.</summary>
/// <param name="Connected">True if the API connection succeeded.</param>
/// <param name="PackagesCount">Total hosting packages on the server, or null if unavailable.</param>
/// <param name="LastTestedAt">UTC timestamp of the last successful connection test, or null.</param>
/// <param name="ErrorMessage">Human-readable error if Connected is false, otherwise null.</param>
public record Cwp7ServerInfoDto(
    bool Connected,
    int? PackagesCount,
    DateTimeOffset? LastTestedAt,
    string? ErrorMessage);
