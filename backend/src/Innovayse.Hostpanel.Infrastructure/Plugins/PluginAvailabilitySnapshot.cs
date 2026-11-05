namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>The available serving plugins as one pass of the activation gates found them, and until when that answer holds.</summary>
/// <param name="PluginIds">The ids of the loaded serving plugins that were switched on and configured, compared ordinally like the registry.</param>
/// <param name="ExpiresAt">The moment, in UTC, after which the answer is read again.</param>
public sealed record PluginAvailabilitySnapshot(IReadOnlySet<string> PluginIds, DateTimeOffset ExpiresAt);
