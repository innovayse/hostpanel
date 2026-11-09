using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Infrastructure.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>Loaded plugins whose manifest declares capabilities and one admin route, for the <c>endpoints</c> capability tests (#818).</summary>
internal static class PluginCapabilitySeeds
{
    /// <summary>The admin route every seeded plugin declares, below its <c>ext</c> prefix.</summary>
    public const string AdminPath = "/stats";

    /// <summary>A loaded plugin of a type, with capabilities and one admin route, as if read from a validated manifest.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="type">The type the manifest declares.</param>
    /// <param name="implementation">The CLR type that answers.</param>
    /// <param name="capabilities">The capabilities the manifest lists.</param>
    /// <returns>The loaded plugin.</returns>
    public static LoadedPlugin Plugin(string id, PluginType type, Type implementation, params string[] capabilities)
    {
        var loaded = PluginActivationTestSeeds.Plugin(id, type, implementation);
        var manifest = new PluginManifest
        {
            Id = loaded.Manifest.Id,
            Name = loaded.Manifest.Name,
            Version = loaded.Manifest.Version,
            Author = loaded.Manifest.Author,
            Description = loaded.Manifest.Description,
            Type = type,
            Category = loaded.Manifest.Category,
            EntryPoint = loaded.Manifest.EntryPoint,
            SdkVersion = loaded.Manifest.SdkVersion,
            Capabilities = capabilities,
            Api = new PluginApi { Admin = [new PluginRoute { Method = PluginRouteMethod.Get, Path = AdminPath }] },
        };

        return new LoadedPlugin(manifest, implementation, PluginViewDocuments.None);
    }
}
