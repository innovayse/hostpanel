namespace Innovayse.Application.Slides.DTOs;

/// <summary>DTO representing a single locale-specific translation of a slide.</summary>
/// <param name="Locale">BCP-47 locale code (e.g. "en", "ru", "hy").</param>
/// <param name="Title">Slide headline.</param>
/// <param name="Tagline">Optional short subtitle.</param>
/// <param name="Description">Optional longer body text.</param>
/// <param name="Features">Optional array of feature strings.</param>
/// <param name="CtaText">Optional CTA button label.</param>
/// <param name="CtaUrl">Optional CTA button link.</param>
public record SlideTranslationDto(
    string Locale,
    string Title,
    string? Tagline,
    string? Description,
    string[]? Features,
    string? CtaText,
    string? CtaUrl);
