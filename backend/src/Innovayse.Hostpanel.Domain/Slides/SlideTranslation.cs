using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Slides;

/// <summary>
/// A locale-specific translation for a <see cref="Slide"/>.
/// Stored in the <c>SlideTranslations</c> table.
/// </summary>
public sealed class SlideTranslation : BaseEntity
{
    /// <summary>Longest a locale code may be: matches the <c>SlideTranslations.Locale</c> column.</summary>
    public const int LocaleMaxLength = 10;

    /// <summary>Longest a title may be: matches the <c>SlideTranslations.Title</c> column.</summary>
    public const int TitleMaxLength = 200;

    /// <summary>Longest a tagline may be: matches the <c>SlideTranslations.Tagline</c> column.</summary>
    public const int TaglineMaxLength = 500;

    /// <summary>Longest a description may be: matches the <c>SlideTranslations.Description</c> column.</summary>
    public const int DescriptionMaxLength = 2000;

    /// <summary>Most bullet points a translation may list.</summary>
    public const int FeaturesMaxCount = 20;

    /// <summary>Longest one bullet point may be.</summary>
    public const int FeatureMaxLength = 200;

    /// <summary>
    /// Width of the <c>SlideTranslations.Features</c> column, which holds the bullet points as a JSON array:
    /// room for <see cref="FeaturesMaxCount"/> points of <see cref="FeatureMaxLength"/> characters with every
    /// character escaped, plus the quotes, commas and brackets.
    /// </summary>
    public const int FeaturesColumnMaxLength = 8192;

    /// <summary>Longest a call-to-action label may be: matches the <c>SlideTranslations.CtaText</c> column.</summary>
    public const int CtaTextMaxLength = 100;

    /// <summary>Longest a call-to-action link may be: matches the <c>SlideTranslations.CtaUrl</c> column.</summary>
    public const int CtaUrlMaxLength = 500;

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

    /// <summary>Gets the optional bullet points listed on the slide, in display order.</summary>
    public string[]? Features { get; private set; }

    /// <summary>Gets the optional CTA button label.</summary>
    public string? CtaText { get; private set; }

    /// <summary>Gets the optional CTA button link.</summary>
    public string? CtaUrl { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private SlideTranslation() { }

    /// <summary>
    /// Creates a new slide translation.
    /// </summary>
    /// <param name="locale">BCP-47 locale code.</param>
    /// <param name="title">Slide headline.</param>
    /// <param name="tagline">Optional subtitle.</param>
    /// <param name="description">Optional body text.</param>
    /// <param name="features">Optional bullet points, in display order.</param>
    /// <param name="ctaText">Optional CTA button label.</param>
    /// <param name="ctaUrl">Optional CTA button link.</param>
    /// <returns>A new <see cref="SlideTranslation"/>.</returns>
    internal static SlideTranslation Create(
        string locale,
        string title,
        string? tagline,
        string? description,
        IReadOnlyList<string>? features,
        string? ctaText,
        string? ctaUrl)
    {
        return new SlideTranslation
        {
            Locale = locale,
            Title = title,
            Tagline = tagline,
            Description = description,
            Features = features?.ToArray(),
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
    /// <param name="features">New bullet points, in display order.</param>
    /// <param name="ctaText">New CTA label.</param>
    /// <param name="ctaUrl">New CTA link.</param>
    internal void Update(
        string title,
        string? tagline,
        string? description,
        IReadOnlyList<string>? features,
        string? ctaText,
        string? ctaUrl)
    {
        Title = title;
        Tagline = tagline;
        Description = description;
        Features = features?.ToArray();
        CtaText = ctaText;
        CtaUrl = ctaUrl;
    }
}
