namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>The registry's JSON for a listing, and for the answer to a newest-version lookup.</summary>
/// <param name="Items">The plugins.</param>
/// <param name="Total">How many plugins match across all pages.</param>
internal sealed record PluginRegistryPageWire(List<PluginRegistryEntryWire>? Items, int Total);
