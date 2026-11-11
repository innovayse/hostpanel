using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Testimonials;

/// <summary>
/// A client testimonial shown on the storefront, with per-locale translations.
/// Stored in the <c>Testimonials</c> table.
/// </summary>
public sealed class Testimonial : BaseEntity, IAggregateRoot, ISortable
{
    /// <summary>Longest Name may be: matches the <c>Testimonials.Name</c> column.</summary>
    public const int NameMaxLength = 200;

    /// <summary>Longest Company may be: matches the <c>Testimonials.Company</c> column.</summary>
    public const int CompanyMaxLength = 200;

    /// <summary>Longest AvatarUrl may be: matches the <c>Testimonials.AvatarUrl</c> column.</summary>
    public const int AvatarUrlMaxLength = 500;

    /// <summary>Lowest star rating allowed.</summary>
    public const int MinRating = 1;

    /// <summary>Highest star rating allowed.</summary>
    public const int MaxRating = 5;

    /// <summary>Gets the client's display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the client's company, or <c>null</c>.</summary>
    public string? Company { get; private set; }

    /// <summary>Gets the URL of the client's avatar image, or <c>null</c>.</summary>
    public string? AvatarUrl { get; private set; }

    /// <summary>Gets the star rating, from 1 to 5.</summary>
    public int Rating { get; private set; }

    /// <summary>Gets the display order (ascending).</summary>
    public int SortOrder { get; private set; }

    /// <summary>Gets whether the testimonial is currently shown on the storefront.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Gets the date the testimonial was given, as set by an admin, or <c>null</c> when none was set
    /// (the storefront then shows <see cref="CreatedAt"/>).
    /// </summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>Backing field for translations.</summary>
    private readonly List<TestimonialTranslation> _translations = [];

    /// <summary>Gets the per-locale translations.</summary>
    public IReadOnlyList<TestimonialTranslation> Translations => _translations.AsReadOnly();

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private Testimonial() { }

    /// <summary>
    /// Creates a new testimonial with no translations yet.
    /// </summary>
    /// <param name="name">The client's display name.</param>
    /// <param name="company">The client's company, or <c>null</c></param>
    /// <param name="avatarUrl">The URL of the client's avatar image, or <c>null</c></param>
    /// <param name="rating">The star rating, from 1 to 5.</param>
    /// <param name="sortOrder">Display order (ascending).</param>
    /// <param name="isActive">Whether the testimonial is shown on the storefront.</param>
    /// <param name="publishedAt">The date the testimonial was given, or <c>null</c> to fall back to the creation time.</param>
    /// <returns>A new <see cref="Testimonial"/>.</returns>
    /// <exception cref="DomainRuleException">The name is empty, the rating is outside 1 to 5, or the sort order is negative.</exception>
    public static Testimonial Create(string name, string? company, string? avatarUrl, int rating, int sortOrder, bool isActive, DateTimeOffset? publishedAt)
    {
        EnsureValid(name, rating, sortOrder);

        return new Testimonial
        {
            Name = name,
            Company = company,
            AvatarUrl = avatarUrl,
            Rating = rating,
            SortOrder = sortOrder,
            IsActive = isActive,
            PublishedAt = publishedAt
        };
    }

    /// <summary>
    /// Updates every non-translation field.
    /// </summary>
    /// <param name="name">The client's display name.</param>
    /// <param name="company">The client's company, or <c>null</c></param>
    /// <param name="avatarUrl">The URL of the client's avatar image, or <c>null</c></param>
    /// <param name="rating">The star rating, from 1 to 5.</param>
    /// <param name="sortOrder">New display order.</param>
    /// <param name="isActive">New active state.</param>
    /// <param name="publishedAt">The date the testimonial was given, or <c>null</c> to fall back to the creation time.</param>
    /// <exception cref="DomainRuleException">The name is empty, the rating is outside 1 to 5, or the sort order is negative.</exception>
    public void Update(string name, string? company, string? avatarUrl, int rating, int sortOrder, bool isActive, DateTimeOffset? publishedAt)
    {
        EnsureValid(name, rating, sortOrder);

        Name = name;
        Company = company;
        AvatarUrl = avatarUrl;
        Rating = rating;
        SortOrder = sortOrder;
        IsActive = isActive;
        PublishedAt = publishedAt;
    }

    /// <summary>Returns the date the storefront shows: <see cref="PublishedAt"/>, or <see cref="CreatedAt"/> when none was set.</summary>
    /// <returns>The display date.</returns>
    public DateTimeOffset GetDisplayDate() => PublishedAt ?? CreatedAt;

    /// <summary>Shows the testimonial on the storefront.</summary>
    public void Activate() => IsActive = true;

    /// <summary>Hides the testimonial from the storefront.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Moves the testimonial to a new position in the display order.</summary>
    /// <param name="sortOrder">The new display order (ascending).</param>
    /// <exception cref="DomainRuleException">The sort order is negative.</exception>
    public void SetSortOrder(int sortOrder)
    {
        EnsureSortOrder(sortOrder);
        SortOrder = sortOrder;
    }

    /// <summary>
    /// Adds or updates the translation for the given locale.
    /// </summary>
    /// <param name="locale">BCP-47 locale code.</param>
    /// <param name="role">The client's job title in this locale, or <c>null</c></param>
    /// <param name="quote">The testimonial text in this locale.</param>
    public void SetTranslation(string locale, string? role, string quote)
    {
        var existing = _translations.FirstOrDefault(t => t.Locale == locale);
        if (existing is not null)
        {
            existing.Update(role, quote);
        }
        else
        {
            _translations.Add(TestimonialTranslation.Create(locale, role, quote));
        }
    }

    /// <summary>
    /// Removes the translation for the given locale, if there is one.
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
    /// Makes the testimonial's translations exactly the given set: locales that are not in it are
    /// removed, the rest are added or updated in place.
    /// </summary>
    /// <param name="translations">The complete set of translations, keyed by BCP-47 locale code.</param>
    /// <exception cref="ArgumentNullException"><paramref name="translations"/> is null.</exception>
    public void ReplaceTranslations(IReadOnlyDictionary<string, TestimonialTranslationContent> translations)
    {
        ArgumentNullException.ThrowIfNull(translations);

        _translations.RemoveAll(t => !translations.ContainsKey(t.Locale));
        foreach (var (locale, content) in translations)
        {
            SetTranslation(locale, content.Role, content.Quote);
        }
    }

    /// <summary>Refuses a negative sort order.</summary>
    /// <param name="sortOrder">The display order to check.</param>
    /// <exception cref="DomainRuleException">The sort order is negative.</exception>
    private static void EnsureSortOrder(int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new DomainRuleException(TestimonialErrors.SortOrderNegative, "A sort order cannot be negative.");
        }
    }

    /// <summary>Refuses input that breaks an invariant shared by creation and update.</summary>
    /// <param name="name">The display name to check.</param>
    /// <param name="rating">The star rating to check.</param>
    /// <param name="sortOrder">The display order to check.</param>
    /// <exception cref="DomainRuleException">The name is empty, the rating is outside 1 to 5, or the sort order is negative.</exception>
    private static void EnsureValid(string name, int rating, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException(TestimonialErrors.NameRequired, "A testimonial needs a name.");
        }

        if (rating < MinRating || rating > MaxRating)
        {
            throw new DomainRuleException(TestimonialErrors.RatingOutOfRange, "A rating runs from 1 to 5.");
        }

        EnsureSortOrder(sortOrder);
    }
}
