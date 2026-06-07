namespace Innovayse.Application.Admin.Integrations.Queries.GetCwp7ServerInfo;

using Innovayse.Domain.Settings.Interfaces;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Caching.Memory;

/// <summary>Handles <see cref="GetCwp7ServerInfoQuery"/> — fetches live CWP7 server status, cached for 5 minutes.</summary>
/// <param name="settings">Setting repository for reading integration credentials.</param>
/// <param name="cwp7Client">CWP7 API client abstraction.</param>
/// <param name="cache">In-memory cache for the server info result.</param>
public sealed class GetCwp7ServerInfoHandler(
    ISettingRepository settings,
    ICwp7ApiClient cwp7Client,
    IMemoryCache cache)
{
    /// <summary>Cache key used to store and retrieve the CWP7 server info result.</summary>
    private const string CacheKey = "cwp7:server-info";

    /// <summary>How long a successful server-info result is cached before being refreshed.</summary>
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Fetches CWP7 server status from the in-memory cache if available, or queries the live API.
    /// Uses Packages LIST to verify connectivity since Account LIST is not enabled.
    /// </summary>
    /// <param name="query">The query (carries no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="Cwp7ServerInfoDto"/> with connection status and package count.
    /// When credentials are missing or the API call fails, <see cref="Cwp7ServerInfoDto.Connected"/> is false.
    /// </returns>
    public async Task<Cwp7ServerInfoDto> HandleAsync(GetCwp7ServerInfoQuery query, CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out Cwp7ServerInfoDto? cached) && cached is not null)
        {
            return cached;
        }

        var hostSetting = await settings.FindByKeyAsync("integration:cwp7:hostname", ct);
        var portSetting = await settings.FindByKeyAsync("integration:cwp7:port", ct);
        var apiKeySetting = await settings.FindByKeyAsync("integration:cwp7:access_hash", ct);

        var host = hostSetting?.Value;
        var port = portSetting?.Value ?? "2304";
        var apiKey = apiKeySetting?.Value;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(apiKey))
        {
            return new Cwp7ServerInfoDto(
                Connected: false,
                PackagesCount: null,
                LastTestedAt: null,
                ErrorMessage: "CWP7 is not configured.");
        }

        try
        {
            var packagesCount = await cwp7Client.GetServerInfoAsync(host, port, apiKey, ct);

            var dto = new Cwp7ServerInfoDto(
                Connected: true,
                PackagesCount: packagesCount,
                LastTestedAt: DateTimeOffset.UtcNow,
                ErrorMessage: null);

            cache.Set(CacheKey, dto, _cacheDuration);
            return dto;
        }
        catch (Exception ex)
        {
            return new Cwp7ServerInfoDto(
                Connected: false,
                PackagesCount: null,
                LastTestedAt: null,
                ErrorMessage: ex.Message);
        }
    }
}
