using Hostpanel.Sdk.Plugins.Manifest;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>What a panel needs of one available plugin to show its pages: the manifest and the view documents.</summary>
/// <param name="Manifest">The manifest, validated at load.</param>
/// <param name="Views">The view documents its pages and slots name, validated at load.</param>
public sealed record PluginUiPackage(PluginManifest Manifest, PluginViewDocuments Views);
