namespace Innovayse.Domain.Slides;

using Innovayse.Domain.Common;

/// <summary>
/// A locale-specific translation for a <see cref="Slide"/>.
/// Stored in the <c>slide_translations</c> table.
/// </summary>
public sealed class SlideTranslation : Entity
{
    /// <summary>Gets the FK to the parent <see cref="Slide"/>.</summary>
    public int SlideId { get; private set; }

    /// <summary>Gets the BCP-47 locale code (e.g. "en", "ru", "hy").</summary>
    public string Locale { get; private set; } = string.Empty;

    /// <summary>Gets the slide headline.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Gets the optional short subtitle.</summary>
    public string? Tagline { get; private set; }

    /// <summary>Gets the optional longer body text.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets the optional JSON array of feature strings.</summary>
    public string? Features { get; private set; }

    /// <summary>Gets the optional CTA button label.</summary>
    public string? CtaText { get; private set; }

    /// <summary>Gets the optional CTA button link.</summary>
    public string? CtaUrl { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private SlideTranslation() : base(0) { }

    /// <summary>
    /// Creates a new slide translation.
    /// </summary>
    /// <param name="locale">BCP-47 locale code.</param>
    /// <param name="title">Slide headline.</param>
    /// <param name="tagline">Optional subtitle.</param>
    /// <param name="description">Optional body text.</param>
    /// <param name="features">Optional JSON array of feature strings.</param>
    /// <param name="ctaText">Optional CTA button label.</param>
    /// <param name="ctaUrl">Optional CTA button link.</param>
    /// <returns>A new <see cref="SlideTranslation"/>.</returns>
    internal static SlideTranslation Create(
        string locale,
        string title,
        string? tagline,
        string? description,
        string? features,
        string? ctaText,
        string? ctaUrl)
    {
        return new SlideTranslation
        {
            Locale = locale,
            Title = title,
            Tagline = tagline,
            Description = description,
            Features = features,
            CtaText = ctaText,
            CtaUrl = ctaUrl
        };
    }

    /// <summary>
    /// Updates all translatable fields.
    /// </summary>
    /// <param name="title">New headline.</param>
    /// <param name="tagline">New subtitle.</param>
    /// <param name="description">New body text.</param>
    /// <param name="features">New JSON features array.</param>
    /// <param name="ctaText">New CTA label.</param>
    /// <param name="ctaUrl">New CTA link.</param>
    internal void Update(
        string title,
        string? tagline,
        string? description,
        string? features,
        string? ctaText,
        string? ctaUrl)
    {
        Title = title;
        Tagline = tagline;
        Description = description;
        Features = features;
        CtaText = ctaText;
        CtaUrl = ctaUrl;
    }
}
