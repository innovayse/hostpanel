using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>A plugin the loader skipped at startup because it breaks a manifest rule, kept so the admin list can show it.</summary>
/// <param name="PluginId">The plugin's id: the name of its folder, which is also what a removal addresses.</param>
/// <param name="Rule">The manifest rule that is broken; <see cref="PluginPackageErrors.CodeFor(PluginManifestRule)"/> gives its error code.</param>
/// <param name="Detail">Where and how the rule is broken: the member of the manifest and its value.</param>
/// <param name="IsBuiltIn">Whether the plugin ships with the build rather than being uploaded.</param>
public sealed record RefusedPlugin(string PluginId, PluginManifestRule Rule, string Detail, bool IsBuiltIn);
