using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Hostpanel.Sdk.Plugins.Manifest;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Represents a plugin that was successfully loaded at startup.
/// </summary>
/// <param name="Manifest">The plugin's parsed manifest.</param>
/// <param name="ImplementationType">The concrete provider type loaded from the DLL.</param>
/// <param name="Views">The view documents its pages and slots name, read and checked at load.</param>
/// <param name="Icon">The icon its manifest names, read and checked at load; <see langword="null"/> when it ships none.</param>
public sealed record LoadedPlugin(PluginManifest Manifest, Type ImplementationType, PluginViewDocuments Views, PluginIconDto? Icon = null);
