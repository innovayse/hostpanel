using System.Runtime.Loader;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Hosting;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Represents a plugin that was successfully loaded: at startup, or by a reload of a newer version.
/// </summary>
/// <param name="Manifest">The plugin's parsed manifest.</param>
/// <param name="ImplementationType">The concrete provider type loaded from the DLL.</param>
/// <param name="Views">The view documents its pages and slots name, read and checked at load.</param>
/// <param name="Icon">The icon its manifest names, read and checked at load; <see langword="null"/> when it ships none.</param>
/// <param name="AgentProfile">The SSH key agent profile its manifest names, read and checked at load; <see langword="null"/> when it ships none or the files were refused.</param>
/// <param name="LoadContext">The collectible context the plugin's assemblies were loaded in; <see langword="null"/> for a plugin the host did not load itself.</param>
public sealed record LoadedPlugin(
    PluginManifest Manifest,
    Type ImplementationType,
    PluginViewDocuments Views,
    PluginIconDto? Icon = null,
    PluginAgentProfileDto? AgentProfile = null,
    AssemblyLoadContext? LoadContext = null)
{
    /// <summary>Whether the host starts the plugin at startup and stops it at shutdown: its manifest says hosted, or its class also implements the hosted contract.</summary>
    public bool IsHosted => Manifest.Type == PluginType.Hosted || typeof(IHostedPlugin).IsAssignableFrom(ImplementationType);

    /// <summary>Counts the instances of this version that are in use, which a reload waits on before it unloads the version.</summary>
    public PluginUsage Usage { get; } = new();
}
