namespace Innovayse.Application.Admin.Integrations.Queries.GetCwpServerInfo;

using Innovayse.Domain.Settings.Interfaces;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Caching.Memory;

/// <summary>Handles <see cref="GetCwpServerInfoQuery"/> — fetches live CWP server status, cached for 5 minutes.</summary>
/// <param name="settings">Setting repository for reading integration credentials.</param>
/// <param name="cwpClient">CWP API client abstraction.</param>
/// <param name="cache">In-memory cache for the server info result.</param>
public sealed class GetCwpServerInfoHandler(
    ISettingRepository settings,
    ICwpApiClient cwpClient,
    IMemoryCache cache)
{
    /// <summary>Cache key used to store and retrieve the CWP server info result.</summary>
    private const string CacheKey = "cwp:server-info";

    /// <summary>How long a successful server-info result is cached before being refreshed.</summary>
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Fetches CWP server status from the in-memory cache if available, or queries the live API.
    /// </summary>
    /// <param name="query">The query (carries no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="CwpServerInfoDto"/> with connection status, account count, and version.
    /// When credentials are missing or the API call fails, <see cref="CwpServerInfoDto.Connected"/> is false.
    /// </returns>
    public async Task<CwpServerInfoDto> HandleAsync(GetCwpServerInfoQuery query, CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out CwpServerInfoDto? cached) && cached is not null)
        {
            return cached;
        }

        var hostSetting = await settings.FindByKeyAsync("integration:innovayse-cwp:host", ct);
        var portSetting = await settings.FindByKeyAsync("integration:innovayse-cwp:port", ct);
        var apiKeySetting = await settings.FindByKeyAsync("integration:innovayse-cwp:api_key", ct);

        var host = hostSetting?.Value;
        var port = portSetting?.Value ?? "2031";
        var apiKey = apiKeySetting?.Value;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(apiKey))
        {
            return new CwpServerInfoDto(
                Connected: false,
                AccountsCount: null,
                CwpVersion: null,
                LastTestedAt: null,
                ErrorMessage: "CWP is not configured.");
        }

        try
        {
            var (accountsCount, cwpVersion) = await cwpClient.GetServerInfoAsync(host, port, apiKey, ct);

            var dto = new CwpServerInfoDto(
                Connected: true,
                AccountsCount: accountsCount,
                CwpVersion: cwpVersion,
                LastTestedAt: DateTimeOffset.UtcNow,
                ErrorMessage: null);

            cache.Set(CacheKey, dto, _cacheDuration);
            return dto;
        }
        catch (Exception ex)
        {
            return new CwpServerInfoDto(
                Connected: false,
                AccountsCount: null,
                CwpVersion: null,
                LastTestedAt: null,
                ErrorMessage: ex.Message);
        }
    }
}
