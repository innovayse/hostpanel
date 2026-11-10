using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/*
 * The one reader of a plugin integration's connection state. The list, the detail page and Test Connection all
 * go through it, so they agree on what a plugin's answer means and share one cache entry per plugin: the list
 * asks every plugin on every load, and without the cache each load would run plugin code once per plugin.
 * The cache holds the language-neutral probe result; the sentence is picked per read, in the request language.
 */

/// <summary>Reads, caches and words the connection state of plugin integrations that implement <c>IPluginStatus</c>.</summary>
public sealed class PluginConnectionReader
{
    /// <summary>Asks the plugin, with the host's time limit and fail-closed handling.</summary>
    private readonly IPluginStatusProbe _probe;

    /// <summary>Holds the last check per plugin for the configured lifetime.</summary>
    private readonly IMemoryCache _cache;

    /// <summary>How long a check stays cached.</summary>
    private readonly IOptions<PluginApiOptions> _options;

    /// <summary>The moment a check is stamped with.</summary>
    private readonly IClock _clock;

    /// <summary>The host's sentences, in the request language.</summary>
    private readonly IErrorMessageResolver _messages;

    /// <summary>Initializes the reader.</summary>
    /// <param name="probe">Asks the plugin, with the host's time limit and fail-closed handling.</param>
    /// <param name="cache">Holds the last check per plugin for the configured lifetime.</param>
    /// <param name="options">How long a check stays cached.</param>
    /// <param name="clock">The moment a check is stamped with.</param>
    /// <param name="messages">The host's sentences, in the request language.</param>
    public PluginConnectionReader(
        IPluginStatusProbe probe,
        IMemoryCache cache,
        IOptions<PluginApiOptions> options,
        IClock clock,
        IErrorMessageResolver messages)
    {
        _probe = probe;
        _cache = cache;
        _options = options;
        _clock = clock;
        _messages = messages;
    }

    /// <summary>Tells whether the plugin can report a connection state, without running plugin code.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns><see langword="true"/> when the plugin implements <c>IPluginStatus</c>.</returns>
    public bool Supports(string pluginId)
    {
        return _probe.Supports(pluginId);
    }

    /// <summary>The plugin's state from the cache, asking the plugin only when nothing fresh is cached.</summary>
    /// <param name="manifest">The plugin's manifest, whose locales word its detail.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The state, or <see langword="null"/> when the plugin does not implement <c>IPluginStatus</c>.</returns>
    public async Task<IntegrationConnectionDto?> ReadAsync(PluginManifest manifest, CancellationToken ct)
    {
        if (!_probe.Supports(manifest.Id))
        {
            return null;
        }

        if (!_cache.TryGetValue<PluginStatusCheck>(PluginStatusCacheKeys.For(manifest.Id), out var check) || check is null)
        {
            check = await CheckAsync(manifest.Id, ct);
        }

        return Word(manifest, check);
    }

    /// <summary>Asks the plugin now, whatever is cached, and caches the answer for the next reads.</summary>
    /// <param name="manifest">The plugin's manifest, whose locales word its detail.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The state, or <see langword="null"/> when the plugin does not implement <c>IPluginStatus</c>.</returns>
    public async Task<IntegrationConnectionDto?> RefreshAsync(PluginManifest manifest, CancellationToken ct)
    {
        if (!_probe.Supports(manifest.Id))
        {
            return null;
        }

        return Word(manifest, await CheckAsync(manifest.Id, ct));
    }

    /// <summary>Runs one check and caches it.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The check.</returns>
    private async Task<PluginStatusCheck> CheckAsync(string pluginId, CancellationToken ct)
    {
        var result = await _probe.ProbeAsync(pluginId, ct);
        var check = new PluginStatusCheck(result, _clock.UtcNow);
        _cache.Set(PluginStatusCacheKeys.For(pluginId), check, _options.Value.StatusCacheLifetime);
        return check;
    }

    /// <summary>Turns a check into the state and sentence the admin reads.</summary>
    /// <param name="manifest">The plugin's manifest, whose locales word its detail.</param>
    /// <param name="check">The check.</param>
    /// <returns>The state.</returns>
    private IntegrationConnectionDto Word(PluginManifest manifest, PluginStatusCheck check)
    {
        var (state, code) = check.Result.Outcome switch
        {
            PluginStatusProbeOutcome.Reported => check.Result.Health switch
            {
                PluginHealth.Healthy => (IntegrationConnectionState.Connected, HostpanelErrorCodes.PLUGIN_STATUS_HEALTHY),
                PluginHealth.Degraded => (IntegrationConnectionState.Degraded, HostpanelErrorCodes.PLUGIN_STATUS_DEGRADED),
                // Unavailable, and any value the adapter should already have refused, read as not working.
                _ => (IntegrationConnectionState.Unavailable, HostpanelErrorCodes.PLUGIN_STATUS_REPORTED_DOWN),
            },
            PluginStatusProbeOutcome.Inactive => (IntegrationConnectionState.Inactive, HostpanelErrorCodes.PLUGIN_STATUS_INACTIVE),
            PluginStatusProbeOutcome.TimedOut => (IntegrationConnectionState.Unavailable, HostpanelErrorCodes.PLUGIN_STATUS_TIMED_OUT),
            // Failed, and NotSupported (a plugin unloaded between Supports and the check): fail closed.
            _ => (IntegrationConnectionState.Unavailable, HostpanelErrorCodes.PLUGIN_STATUS_FAILED),
        };

        // The plugin's own detail is shown only with a report it made; host outcomes always use the host's sentence.
        var detail = check.Result.Outcome == PluginStatusProbeOutcome.Reported && check.Result.Detail is { } own
            ? PluginText.Resolve(manifest, own)
            : null;

        return new IntegrationConnectionDto(state, detail ?? _messages.Resolve(code), check.CheckedAt);
    }
}
