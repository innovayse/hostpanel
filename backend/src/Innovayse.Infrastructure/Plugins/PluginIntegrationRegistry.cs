namespace Innovayse.Infrastructure.Plugins;

using Innovayse.Application.Admin.Integrations;

/// <summary>In-memory registry of plugins that expose themselves as integrations.</summary>
public sealed class PluginIntegrationRegistry : IPluginIntegrationRegistry
{
    /// <summary>Thread-safe list of registered plugin integration entries.</summary>
    private readonly List<PluginIntegrationEntry> _entries = [];

    /// <inheritdoc/>
    public void Register(PluginIntegrationEntry entry) => _entries.Add(entry);

    /// <inheritdoc/>
    public IReadOnlyList<PluginIntegrationEntry> GetAll() => _entries.AsReadOnly();

    /// <inheritdoc/>
    public bool IsRegistered(string slug) =>
        _entries.Any(e => e.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
}
