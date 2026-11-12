namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>The reviews summary of a registry plugin, as the registry computes it.</summary>
/// <param name="Average">The average star rating, between 1 and 5.</param>
/// <param name="Count">The number of reviews behind the average, at least 1.</param>
public sealed record RegistryPluginRatingDto(double Average, int Count);
