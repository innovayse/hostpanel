namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The durable record that an upgrade was swapped in and its migration hook has not run yet. It is written when the
/// swap happens at startup and deleted once the hook succeeded and the migration was audited, so the hook runs once.
/// </summary>
/// <param name="PluginId">The plugin that was upgraded.</param>
/// <param name="FromVersion">The version that was replaced; <see langword="null"/> when its manifest could not be read.</param>
/// <param name="ToVersion">The version now installed.</param>
internal sealed record PluginUpgradeMarker(string PluginId, string? FromVersion, string ToVersion);
