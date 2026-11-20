namespace Innovayse.Hostpanel.Application.Slides.Common;

/// <summary>DTO representing a single locale-specific translation of a slide.</summary>
/// <param name="Locale">BCP-47 locale code (e.g. "en", "ru", "hy", "de", "es").</param>
/// <param name="Title">Slide headline.</param>
/// <param name="Tagline">Optional short subtitle.</param>
/// <param name="Description">Optional longer body text.</param>
/// <param name="Features">Optional array of feature strings.</param>
/// <param name="CtaText">Optional CTA button label.</param>
/// <param name="CtaUrl">Optional CTA button link.</param>
public sealed record SlideTranslationDto(
    string Locale,
    string Title,
    string? Tagline,
    string? Description,
    string[]? Features,
    string? CtaText,
    string? CtaUrl);
