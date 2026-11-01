using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>One plugin found on disk by <see cref="Innovayse.Hostpanel.Application.Common.Interfaces.IPluginPackageStore.ListAsync"/>.</summary>
/// <param name="Manifest">The plugin's parsed <c>plugin.json</c>.</param>
/// <param name="IsBuiltIn">Whether the plugin ships with the build rather than being uploaded; a built-in plugin cannot be removed.</param>
public sealed record InstalledPluginPackage(PluginManifest Manifest, bool IsBuiltIn);
