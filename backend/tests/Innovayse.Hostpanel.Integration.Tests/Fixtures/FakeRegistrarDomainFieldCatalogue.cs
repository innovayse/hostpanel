using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IRegistrarDomainFieldCatalogue"/> over manifests a test builds, so a test can say which extra domain fields a
/// registrar plugin declares for a zone without installing a plugin. The zones are invented per test: nothing here knows a
/// real extension, the host takes every zone from the manifest.
/// </summary>
public sealed class FakeRegistrarDomainFieldCatalogue : IRegistrarDomainFieldCatalogue
{
    /// <summary>The manifests by module, compared without regard to letter case as the real catalogue compares ids.</summary>
    private readonly Dictionary<string, PluginManifest> _manifests = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Builds a registrar manifest whose <c>domainFields</c> hold the given sets and whose locales hold the given texts.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="sets">The sets of domain fields.</param>
    /// <param name="locales">The locale texts by language, or <see langword="null"/> for none.</param>
    /// <returns>The manifest.</returns>
    public static PluginManifest Manifest(
        string id, IReadOnlyList<PluginDomainFieldSet> sets, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? locales = null)
    {
        return new PluginManifest
        {
            Id = id,
            Name = "Field registrar",
            Version = "1.0.0",
            Author = "Tests",
            Description = "Registrar manifest used by the domain field tests.",
            Type = PluginType.Registrar,
            Category = "Registrar",
            EntryPoint = "Tests.FieldRegistrar",
            SdkVersion = "3.17.0",
            ManifestVersion = 2,
            DomainFields = sets,
            Locales = locales ?? new Dictionary<string, IReadOnlyDictionary<string, string>>(),
        };
    }

    /// <summary>Builds one field of a set.</summary>
    /// <param name="key">The field key.</param>
    /// <param name="type">The input kind.</param>
    /// <param name="required">Whether the field must have a value.</param>
    /// <returns>The field.</returns>
    public static PluginDomainField Field(string key, PluginDomainFieldType type = PluginDomainFieldType.Text, bool required = true)
    {
        return new PluginDomainField { Key = key, Label = key, Type = type, Required = required };
    }

    /// <summary>Makes a module name the given manifest.</summary>
    /// <param name="module">The module value stored on a TLD or a domain.</param>
    /// <param name="manifest">The manifest of the plugin that module names.</param>
    public void Add(string module, PluginManifest manifest)
    {
        _manifests[module] = manifest;
    }

    /// <inheritdoc/>
    public PluginManifest? ManifestOf(string? module)
    {
        return module is not null && _manifests.TryGetValue(module, out var manifest) ? manifest : null;
    }
}
