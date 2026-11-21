using Innovayse.Hostpanel.Application.Faqs.Common;
using Innovayse.Hostpanel.Domain.Faqs.Enums;

namespace Innovayse.Hostpanel.Application.Faqs.Commands.UpdateFaq;

/// <summary>Command to update an existing question and replace its translations.</summary>
/// <param name="Id">
/// The question identifier to update; wins over any id in the body. A value type, so it is left
/// non-nullable: a missing <c>int</c> in the body binds to <c>0</c> and the route overlay
/// (<c>command with { Id = id }</c>) replaces it.
/// </param>
/// <param name="Category">The topic the question belongs to.</param>
/// <param name="SortOrder">Display order (ascending).</param>
/// <param name="IsActive">Whether the question is shown on the storefront.</param>
/// <param name="Translations">Per-locale translations keyed by locale code (<c>en</c>, <c>ru</c>, <c>hy</c>, <c>de</c>, <c>es</c>).</param>
public sealed record UpdateFaqCommand(
    int Id,
    FaqCategory Category,
    int SortOrder,
    bool IsActive,
    Dictionary<string, FaqTranslationDto> Translations);
