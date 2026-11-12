namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>The registry's JSON for the reviews summary of a plugin (<c>rating</c>). Every member is checked before it becomes an application type.</summary>
/// <param name="Average">The average star rating.</param>
/// <param name="Count">The number of reviews.</param>
internal sealed record PluginRegistryRatingWire(double? Average, int? Count);
