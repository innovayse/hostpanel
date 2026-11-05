using Hostpanel.Sdk.Plugins.Manifest;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>One plugin found on disk by <see cref="Innovayse.Hostpanel.Application.Common.Interfaces.IPluginPackageStore.ListAsync"/>.</summary>
/// <param name="Manifest">The plugin's parsed <c>plugin.json</c>.</param>
/// <param name="IsBuiltIn">Whether the plugin ships with the build rather than being uploaded; a built-in plugin cannot be removed.</param>
/// <param name="FolderName">
/// The name of the plugin's folder, which is the id a removal addresses and the id a refusal is recorded under; it
/// equals the manifest id except in a folder placed by hand.
/// </param>
/// <param name="StagedVersion">The version of an upload staged to replace this plugin at the next restart; <see langword="null"/> when none.</param>
public sealed record InstalledPluginPackage(PluginManifest Manifest, bool IsBuiltIn, string FolderName, string? StagedVersion = null);
