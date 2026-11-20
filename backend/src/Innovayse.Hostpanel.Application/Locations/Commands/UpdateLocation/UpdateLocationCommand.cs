using Innovayse.Hostpanel.Application.Locations.Common;

namespace Innovayse.Hostpanel.Application.Locations.Commands.UpdateLocation;

/// <summary>Command to update an existing location and replace its translations.</summary>
/// <param name="Id">
/// The location identifier to update; wins over any id in the body. A value type, so it is left
/// non-nullable: a missing <c>int</c> in the body binds to <c>0</c> and the route overlay
/// (<c>command with { Id = id }</c>) replaces it.
/// </param>
/// <param name="CountryCode">ISO 3166 alpha-2 country code (two letters, any case).</param>
/// <param name="SortOrder">Display order (ascending).</param>
/// <param name="IsActive">Whether the location is shown on the storefront.</param>
/// <param name="Translations">Per-locale translations keyed by locale code (<c>en</c>, <c>ru</c>, <c>hy</c>, <c>de</c>, <c>es</c>).</param>
public sealed record UpdateLocationCommand(
    int Id,
    string CountryCode,
    int SortOrder,
    bool IsActive,
    Dictionary<string, LocationTranslationDto> Translations);
