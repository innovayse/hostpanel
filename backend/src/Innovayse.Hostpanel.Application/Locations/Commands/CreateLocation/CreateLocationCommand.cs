using Innovayse.Hostpanel.Application.Locations.Common;

namespace Innovayse.Hostpanel.Application.Locations.Commands.CreateLocation;

/// <summary>Command to create a server location with one or more locale translations.</summary>
/// <param name="CountryCode">ISO 3166 alpha-2 country code (two letters, any case).</param>
/// <param name="SortOrder">Display order (ascending).</param>
/// <param name="IsActive">Whether the location is shown on the storefront.</param>
/// <param name="Translations">Per-locale translations keyed by locale code (<c>en</c>, <c>ru</c>, <c>hy</c>, <c>de</c>, <c>es</c>).</param>
public sealed record CreateLocationCommand(
    string CountryCode,
    int SortOrder,
    bool IsActive,
    Dictionary<string, LocationTranslationDto> Translations);
