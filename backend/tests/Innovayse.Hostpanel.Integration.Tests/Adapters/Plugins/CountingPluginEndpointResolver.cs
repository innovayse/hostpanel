using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.SDK.Plugins.Endpoints;
using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// Wraps the real plugin endpoint resolver and counts <see cref="PrepareAsync"/> calls, the step that reads the plugin's
/// switch and settings from the database, so a test can prove a refusal never reaches it. An external port wrapper, not a
/// mock: every call is passed to the real resolver.
/// </summary>
public sealed class CountingPluginEndpointResolver : IPluginEndpointResolver
{
    /// <summary>The real resolver every call is passed to.</summary>
    private readonly PluginEndpointResolver _inner;

    /// <summary>The counter of <see cref="PrepareAsync"/> calls, shared with the test that reads it.</summary>
    private readonly PluginPrepareCounter _prepared;

    /// <summary>Initializes a new instance of the <see cref="CountingPluginEndpointResolver"/> class.</summary>
    /// <param name="inner">The real resolver every call is passed to.</param>
    /// <param name="prepared">The counter the test reads.</param>
    public CountingPluginEndpointResolver(PluginEndpointResolver inner, PluginPrepareCounter prepared)
    {
        _inner = inner;
        _prepared = prepared;
    }

    /// <inheritdoc/>
    public PluginManifest? Find(string pluginId)
    {
        return _inner.Find(pluginId);
    }

    /// <inheritdoc/>
    public Task<Func<IPluginEndpoints>?> PrepareAsync(string pluginId, CancellationToken ct)
    {
        _prepared.Increment();
        return _inner.PrepareAsync(pluginId, ct);
    }
}
