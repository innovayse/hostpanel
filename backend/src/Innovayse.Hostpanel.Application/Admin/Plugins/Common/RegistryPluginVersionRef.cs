namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>Names one version of one plugin in the registry.</summary>
/// <param name="PluginId">The plugin id.</param>
/// <param name="Version">The version, a strict <c>major.minor.patch</c>.</param>
public sealed record RegistryPluginVersionRef(string PluginId, string Version);
