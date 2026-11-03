using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Represents a plugin that was successfully loaded at startup.
/// </summary>
/// <param name="Manifest">The plugin's parsed manifest.</param>
/// <param name="ImplementationType">The concrete provider type loaded from the DLL.</param>
/// <param name="Views">The view documents its pages and slots name, read and checked at load.</param>
public sealed record LoadedPlugin(PluginManifest Manifest, Type ImplementationType, PluginViewDocuments Views);
