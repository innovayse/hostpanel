using Innovayse.Hostpanel.Application.Partners.Common;

namespace Innovayse.Hostpanel.Application.Partners.Commands.CreatePartner;

/// <summary>Command to create a partner with one or more locale translations.</summary>
/// <param name="LogoUrl">The URL of the partner's logo image, or <c>null</c></param>
/// <param name="Url">The partner's website, or <c>null</c></param>
/// <param name="SortOrder">Display order (ascending).</param>
/// <param name="IsActive">Whether the partner is shown on the storefront.</param>
/// <param name="Translations">Per-locale translations keyed by locale code (<c>en</c>, <c>ru</c>, <c>hy</c>, <c>de</c>, <c>es</c>).</param>
public sealed record CreatePartnerCommand(
    string? LogoUrl,
    string? Url,
    int SortOrder,
    bool IsActive,
    Dictionary<string, PartnerTranslationDto> Translations);
