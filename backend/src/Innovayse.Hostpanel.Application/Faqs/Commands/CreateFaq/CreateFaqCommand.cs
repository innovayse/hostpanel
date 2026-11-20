using Innovayse.Hostpanel.Application.Faqs.Common;
using Innovayse.Hostpanel.Domain.Faqs.Enums;

namespace Innovayse.Hostpanel.Application.Faqs.Commands.CreateFaq;

/// <summary>Command to create a question with one or more locale translations.</summary>
/// <param name="Category">The topic the question belongs to.</param>
/// <param name="SortOrder">Display order (ascending).</param>
/// <param name="IsActive">Whether the question is shown on the storefront.</param>
/// <param name="Translations">Per-locale translations keyed by locale code (<c>en</c>, <c>ru</c>, <c>hy</c>, <c>de</c>, <c>es</c>).</param>
public sealed record CreateFaqCommand(
    FaqCategory Category,
    int SortOrder,
    bool IsActive,
    Dictionary<string, FaqTranslationDto> Translations);
