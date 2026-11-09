using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Partners;

/// <summary>
/// A partner or client logo shown on the storefront, with per-locale translations.
/// Stored in the <c>Partners</c> table.
/// </summary>
public sealed class Partner : BaseEntity, IAggregateRoot, ISortable
{
    /// <summary>Longest LogoUrl may be: matches the <c>Partners.LogoUrl</c> column.</summary>
    public const int LogoUrlMaxLength = 500;

    /// <summary>Longest Url may be: matches the <c>Partners.Url</c> column.</summary>
    public const int UrlMaxLength = 500;

    /// <summary>Gets the URL of the partner's logo image, or <c>null</c>.</summary>
    public string? LogoUrl { get; private set; }

    /// <summary>Gets the partner's website, or <c>null</c>.</summary>
    public string? Url { get; private set; }

    /// <summary>Gets the display order (ascending).</summary>
    public int SortOrder { get; private set; }

    /// <summary>Gets whether the partner is currently shown on the storefront.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Backing field for translations.</summary>
    private readonly List<PartnerTranslation> _translations = [];

    /// <summary>Gets the per-locale translations.</summary>
    public IReadOnlyList<PartnerTranslation> Translations => _translations.AsReadOnly();

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private Partner() { }

    /// <summary>
    /// Creates a new partner with no translations yet.
    /// </summary>
    /// <param name="logoUrl">The URL of the partner's logo image, or <c>null</c></param>
    /// <param name="url">The partner's website, or <c>null</c></param>
    /// <param name="sortOrder">Display order (ascending).</param>
    /// <param name="isActive">Whether the partner is shown on the storefront.</param>
    /// <returns>A new <see cref="Partner"/>.</returns>
    /// <exception cref="DomainRuleException">The sort order is negative.</exception>
    public static Partner Create(string? logoUrl, string? url, int sortOrder, bool isActive)
    {
        EnsureSortOrder(sortOrder);

        return new Partner
        {
            LogoUrl = logoUrl,
            Url = url,
            SortOrder = sortOrder,
            IsActive = isActive,
        };
    }

    /// <summary>
    /// Updates every non-translation field.
    /// </summary>
    /// <param name="logoUrl">The URL of the partner's logo image, or <c>null</c></param>
    /// <param name="url">The partner's website, or <c>null</c></param>
    /// <param name="sortOrder">New display order.</param>
    /// <param name="isActive">New active state.</param>
    /// <exception cref="DomainRuleException">The sort order is negative.</exception>
    public void Update(string? logoUrl, string? url, int sortOrder, bool isActive)
    {
        EnsureSortOrder(sortOrder);

        LogoUrl = logoUrl;
        Url = url;
        SortOrder = sortOrder;
        IsActive = isActive;
    }

    /// <summary>Shows the partner on the storefront.</summary>
    public void Activate() => IsActive = true;

    /// <summary>Hides the partner from the storefront.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Moves the partner to a new position in the display order.</summary>
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
    /// <param name="name">The partner's name in this locale.</param>
    public void SetTranslation(string locale, string name)
    {
        var existing = _translations.FirstOrDefault(t => t.Locale == locale);
        if (existing is not null)
        {
            existing.Update(name);
        }
        else
        {
            _translations.Add(PartnerTranslation.Create(locale, name));
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
    /// Makes the partner's translations exactly the given set: locales that are not in it are
    /// removed, the rest are added or updated in place.
    /// </summary>
    /// <param name="translations">The complete set of translations, keyed by BCP-47 locale code.</param>
    /// <exception cref="ArgumentNullException"><paramref name="translations"/> is null.</exception>
    public void ReplaceTranslations(IReadOnlyDictionary<string, PartnerTranslationContent> translations)
    {
        ArgumentNullException.ThrowIfNull(translations);

        _translations.RemoveAll(t => !translations.ContainsKey(t.Locale));
        foreach (var (locale, content) in translations)
        {
            SetTranslation(locale, content.Name);
        }
    }

    /// <summary>Refuses a negative sort order.</summary>
    /// <param name="sortOrder">The display order to check.</param>
    /// <exception cref="DomainRuleException">The sort order is negative.</exception>
    private static void EnsureSortOrder(int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new DomainRuleException(PartnerErrors.SortOrderNegative, "A sort order cannot be negative.");
        }
    }
}
