using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>One folder the loader scans for plugin folders, with where its plugins come from.</summary>
/// <param name="Path">The absolute folder.</param>
/// <param name="Origin">Whether its plugins ship with the host or were uploaded.</param>
public sealed record PluginRoot(string Path, PluginOrigin Origin);
