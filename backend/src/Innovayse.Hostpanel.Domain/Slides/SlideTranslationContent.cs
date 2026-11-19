namespace Innovayse.Hostpanel.Domain.Slides;

/// <summary>The text of one locale of a slide, handed to <see cref="Slide.ReplaceTranslations"/>.</summary>
/// <param name="Locale">BCP-47 locale code the text is written in (e.g. "en", "ru", "hy", "de", "es").</param>
/// <param name="Title">The slide headline in this locale.</param>
/// <param name="Tagline">The short subtitle under the headline; null when the slide shows none.</param>
/// <param name="Description">The longer body text; null when the slide shows none.</param>
/// <param name="Features">The bullet points listed on the slide, in display order; null when the slide lists none.</param>
/// <param name="CtaText">The call-to-action button label; null when the slide has no button.</param>
/// <param name="CtaUrl">The link the call-to-action button opens; null when the slide has no button.</param>
public sealed record SlideTranslationContent(
    string Locale,
    string Title,
    string? Tagline,
    string? Description,
    IReadOnlyList<string>? Features,
    string? CtaText,
    string? CtaUrl);
