using Innovayse.Hostpanel.Application.Partners.Common;

namespace Innovayse.Hostpanel.Application.Partners.Commands.UpdatePartner;

/// <summary>Command to update an existing partner and replace its translations.</summary>
/// <param name="Id">
/// The partner identifier to update; wins over any id in the body. A value type, so it is left
/// non-nullable: a missing <c>int</c> in the body binds to <c>0</c> and the route overlay
/// (<c>command with { Id = id }</c>) replaces it.
/// </param>
/// <param name="LogoUrl">The URL of the partner's logo image, or <c>null</c></param>
/// <param name="Url">The partner's website, or <c>null</c></param>
/// <param name="SortOrder">Display order (ascending).</param>
/// <param name="IsActive">Whether the partner is shown on the storefront.</param>
/// <param name="Translations">Per-locale translations keyed by locale code (<c>en</c>, <c>ru</c>, <c>hy</c>, <c>de</c>, <c>es</c>).</param>
public sealed record UpdatePartnerCommand(
    int Id,
    string? LogoUrl,
    string? Url,
    int SortOrder,
    bool IsActive,
    Dictionary<string, PartnerTranslationDto> Translations);
