namespace Innovayse.Application.Slides.Queries.ListActiveSlides;

using Innovayse.Domain.Slides;

/// <summary>Query to retrieve active slides for public display, filtered by locale and optional audience.</summary>
/// <param name="Locale">BCP-47 locale code for translation resolution (e.g. "en", "ru", "hy").</param>
/// <param name="Audience">Optional audience filter. When null, all audiences are returned.</param>
public record ListActiveSlidesQuery(string Locale, SlideAudience? Audience);
