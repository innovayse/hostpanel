namespace Innovayse.Domain.Slides;

using Innovayse.Domain.Common;
using Innovayse.Domain.Slides.Events;

/// <summary>
/// A homepage slider slide with per-locale translations.
/// Stored in the <c>slides</c> table.
/// </summary>
public sealed class Slide : AggregateRoot
{
    /// <summary>Gets the MDI icon name (e.g. "cloud-check").</summary>
    public string IconName { get; private set; } = string.Empty;

    /// <summary>Gets the hex brand color (e.g. "#0ea5e9").</summary>
    public string BrandColor { get; private set; } = string.Empty;

    /// <summary>Gets the image URL or path.</summary>
    public string ImageUrl { get; private set; } = string.Empty;

    /// <summary>Gets the optional external demo link.</summary>
    public string? DemoUrl { get; private set; }

    /// <summary>Gets the optional learn-more page link.</summary>
    public string? LearnMoreUrl { get; private set; }

    /// <summary>Gets the optional FK to a Product for pricing data.</summary>
    public int? ProductId { get; private set; }

    /// <summary>Gets the display order (ascending).</summary>
    public int SortOrder { get; private set; }

    /// <summary>Gets whether the slide is currently active.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the target audience for this slide.</summary>
    public SlideAudience TargetAudience { get; private set; }

    /// <summary>Gets the optional start of the visibility window.</summary>
    public DateTimeOffset? VisibleFrom { get; private set; }

    /// <summary>Gets the optional end of the visibility window.</summary>
    public DateTimeOffset? VisibleUntil { get; private set; }

    /// <summary>Gets the UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Backing field for translations.</summary>
    private readonly List<SlideTranslation> _translations = [];

    /// <summary>Gets the per-locale translations.</summary>
    public IReadOnlyList<SlideTranslation> Translations => _translations.AsReadOnly();

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Slide() : base(0) { }

    /// <summary>
    /// Creates a new active slide and raises <see cref="SlideCreatedEvent"/>.
    /// </summary>
    /// <param name="iconName">MDI icon name.</param>
    /// <param name="brandColor">Hex brand color.</param>
    /// <param name="imageUrl">Image URL or path.</param>
    /// <param name="demoUrl">Optional external demo link.</param>
    /// <param name="learnMoreUrl">Optional learn-more page link.</param>
    /// <param name="productId">Optional FK to a Product for pricing data.</param>
    /// <param name="sortOrder">Display order (ascending).</param>
    /// <param name="isActive">Whether the slide is active.</param>
    /// <param name="targetAudience">Target audience for this slide.</param>
    /// <param name="visibleFrom">Optional start of the visibility window.</param>
    /// <param name="visibleUntil">Optional end of the visibility window.</param>
    /// <returns>A new <see cref="Slide"/>.</returns>
    public static Slide Create(
        string iconName, string brandColor, string imageUrl,
        string? demoUrl, string? learnMoreUrl, int? productId,
        int sortOrder, bool isActive, SlideAudience targetAudience,
        DateTimeOffset? visibleFrom, DateTimeOffset? visibleUntil)
    {
        var slide = new Slide
        {
            IconName = iconName,
            BrandColor = brandColor,
            ImageUrl = imageUrl,
            DemoUrl = demoUrl,
            LearnMoreUrl = learnMoreUrl,
            ProductId = productId,
            SortOrder = sortOrder,
            IsActive = isActive,
            TargetAudience = targetAudience,
            VisibleFrom = visibleFrom,
            VisibleUntil = visibleUntil,
            CreatedAt = DateTimeOffset.UtcNow
        };
        slide.AddDomainEvent(new SlideCreatedEvent(0, iconName));
        return slide;
    }

    /// <summary>
    /// Updates all non-translation fields.
    /// </summary>
    /// <param name="iconName">New MDI icon name.</param>
    /// <param name="brandColor">New hex brand color.</param>
    /// <param name="imageUrl">New image URL or path.</param>
    /// <param name="demoUrl">New optional external demo link.</param>
    /// <param name="learnMoreUrl">New optional learn-more page link.</param>
    /// <param name="productId">New optional FK to a Product.</param>
    /// <param name="sortOrder">New display order.</param>
    /// <param name="isActive">New active state.</param>
    /// <param name="targetAudience">New target audience.</param>
    /// <param name="visibleFrom">New optional start of the visibility window.</param>
    /// <param name="visibleUntil">New optional end of the visibility window.</param>
    public void Update(
        string iconName, string brandColor, string imageUrl,
        string? demoUrl, string? learnMoreUrl, int? productId,
        int sortOrder, bool isActive, SlideAudience targetAudience,
        DateTimeOffset? visibleFrom, DateTimeOffset? visibleUntil)
    {
        IconName = iconName;
        BrandColor = brandColor;
        ImageUrl = imageUrl;
        DemoUrl = demoUrl;
        LearnMoreUrl = learnMoreUrl;
        ProductId = productId;
        SortOrder = sortOrder;
        IsActive = isActive;
        TargetAudience = targetAudience;
        VisibleFrom = visibleFrom;
        VisibleUntil = visibleUntil;
    }

    /// <summary>Marks the slide as active.</summary>
    public void Activate() => IsActive = true;

    /// <summary>Marks the slide as inactive.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Adds or updates a translation for the given locale.
    /// </summary>
    /// <param name="locale">BCP-47 locale code.</param>
    /// <param name="title">Slide headline.</param>
    /// <param name="tagline">Optional subtitle.</param>
    /// <param name="description">Optional body text.</param>
    /// <param name="features">Optional JSON array of feature strings.</param>
    /// <param name="ctaText">Optional CTA button label.</param>
    /// <param name="ctaUrl">Optional CTA button link.</param>
    public void SetTranslation(string locale, string title, string? tagline, string? description, string? features, string? ctaText, string? ctaUrl)
    {
        var existing = _translations.FirstOrDefault(t => t.Locale == locale);
        if (existing is not null)
        {
            existing.Update(title, tagline, description, features, ctaText, ctaUrl);
        }
        else
        {
            _translations.Add(SlideTranslation.Create(locale, title, tagline, description, features, ctaText, ctaUrl));
        }
    }

    /// <summary>
    /// Removes the translation for the given locale.
    /// </summary>
    /// <param name="locale">BCP-47 locale code of the translation to remove.</param>
    public void RemoveTranslation(string locale)
    {
        var existing = _translations.FirstOrDefault(t => t.Locale == locale);
        if (existing is not null)
        {
            _translations.Remove(existing);
        }
    }

    /// <summary>
    /// Checks whether this slide should be visible at the given time.
    /// </summary>
    /// <param name="now">The point in time to check visibility against.</param>
    /// <returns><c>true</c> if the slide is active and within its visibility window.</returns>
    public bool IsVisibleAt(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return false;
        }

        if (VisibleFrom.HasValue && now < VisibleFrom.Value)
        {
            return false;
        }

        if (VisibleUntil.HasValue && now > VisibleUntil.Value)
        {
            return false;
        }

        return true;
    }
}
