namespace Innovayse.Infrastructure.Plugins;
using Innovayse.SDK.Plugins;

/// <summary>
/// Represents a plugin that was successfully loaded at startup.
/// </summary>
/// <param name="Manifest">The plugin's parsed manifest.</param>
/// <param name="ImplementationType">The concrete provider type loaded from the DLL.</param>
public sealed record LoadedPlugin(PluginManifest Manifest, Type ImplementationType);
