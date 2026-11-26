using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>In-memory registry of plugins that expose themselves as integrations.</summary>
public sealed class PluginIntegrationRegistry : IPluginIntegrationRegistry
{
    /// <summary>Guards <see cref="_entries"/>: a reload changes an entry while the integration list reads them.</summary>
    private readonly object _gate = new();

    /// <summary>The registered plugin integration entries.</summary>
    private readonly List<PluginIntegrationEntry> _entries = [];

    /// <inheritdoc/>
    public void Register(PluginIntegrationEntry entry)
    {
        lock (_gate)
        {
            _entries.RemoveAll(e => e.Slug.Equals(entry.Slug, StringComparison.OrdinalIgnoreCase));
            _entries.Add(entry);
        }
    }

    /// <inheritdoc/>
    public void Remove(string slug)
    {
        lock (_gate)
        {
            _entries.RemoveAll(e => e.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<PluginIntegrationEntry> GetAll()
    {
        lock (_gate)
        {
            return [.. _entries];
        }
    }

    /// <inheritdoc/>
    public bool IsRegistered(string slug)
    {
        lock (_gate)
        {
            return _entries.Any(e => e.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
        }
    }
}
