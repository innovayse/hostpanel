using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Projects;

/// <summary>
/// A portfolio project shown on the storefront, with per-locale translations.
/// Stored in the <c>Projects</c> table.
/// </summary>
public sealed class Project : BaseEntity, IAggregateRoot, ISortable
{
    /// <summary>Longest Slug may be: matches the <c>Projects.Slug</c> column.</summary>
    public const int SlugMaxLength = 100;

    /// <summary>Longest Url may be: matches the <c>Projects.Url</c> column.</summary>
    public const int UrlMaxLength = 500;

    /// <summary>Most images a project may carry.</summary>
    public const int MaxImages = 10;

    /// <summary>Longest a single image URL may be.</summary>
    public const int ImageUrlMaxLength = 500;

    /// <summary>The shape a slug must have: lowercase letters and digits, single hyphens between them.</summary>
    public const string SlugPattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";

    /// <summary>Gets the project's URL-safe identifier.</summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>Gets the live project's website, or <c>null</c>.</summary>
    public string? Url { get; private set; }

    /// <summary>Gets the project's image URLs, in display order.</summary>
    public string[] ImageUrls { get; private set; } = [];

    /// <summary>Gets the display order (ascending).</summary>
    public int SortOrder { get; private set; }

    /// <summary>Gets whether the project is currently shown on the storefront.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Backing field for translations.</summary>
    private readonly List<ProjectTranslation> _translations = [];

    /// <summary>Gets the per-locale translations.</summary>
    public IReadOnlyList<ProjectTranslation> Translations => _translations.AsReadOnly();

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private Project() { }

    /// <summary>
    /// Creates a new project with no translations yet.
    /// </summary>
    /// <param name="slug">The project's URL-safe identifier.</param>
    /// <param name="url">The live project's website, or <c>null</c></param>
    /// <param name="imageUrls">The project's image URLs, in display order.</param>
    /// <param name="sortOrder">Display order (ascending).</param>
    /// <param name="isActive">Whether the project is shown on the storefront.</param>
    /// <returns>A new <see cref="Project"/>.</returns>
    /// <exception cref="DomainRuleException">The slug is empty or the sort order is negative.</exception>
    public static Project Create(string slug, string? url, IReadOnlyList<string> imageUrls, int sortOrder, bool isActive)
    {
        EnsureValid(slug, sortOrder);

        return new Project
        {
            Slug = slug,
            Url = url,
            ImageUrls = imageUrls.ToArray(),
            SortOrder = sortOrder,
            IsActive = isActive,
        };
    }

    /// <summary>
    /// Updates every non-translation field.
    /// </summary>
    /// <param name="slug">The project's URL-safe identifier.</param>
    /// <param name="url">The live project's website, or <c>null</c></param>
    /// <param name="imageUrls">The project's image URLs, in display order.</param>
    /// <param name="sortOrder">New display order.</param>
    /// <param name="isActive">New active state.</param>
    /// <exception cref="DomainRuleException">The slug is empty or the sort order is negative.</exception>
    public void Update(string slug, string? url, IReadOnlyList<string> imageUrls, int sortOrder, bool isActive)
    {
        EnsureValid(slug, sortOrder);

        Slug = slug;
        Url = url;
        ImageUrls = imageUrls.ToArray();
        SortOrder = sortOrder;
        IsActive = isActive;
    }

    /// <summary>Shows the project on the storefront.</summary>
    public void Activate() => IsActive = true;

    /// <summary>Hides the project from the storefront.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Moves the project to a new position in the display order.</summary>
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
    /// <param name="title">The project's title in this locale.</param>
    /// <param name="summary">A one-line summary in this locale, or <c>null</c></param>
    /// <param name="description">The long description in this locale, or <c>null</c></param>
    public void SetTranslation(string locale, string title, string? summary, string? description)
    {
        var existing = _translations.FirstOrDefault(t => t.Locale == locale);
        if (existing is not null)
        {
            existing.Update(title, summary, description);
        }
        else
        {
            _translations.Add(ProjectTranslation.Create(locale, title, summary, description));
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
    /// Makes the project's translations exactly the given set: locales that are not in it are
    /// removed, the rest are added or updated in place.
    /// </summary>
    /// <param name="translations">The complete set of translations, keyed by BCP-47 locale code.</param>
    /// <exception cref="ArgumentNullException"><paramref name="translations"/> is null.</exception>
    public void ReplaceTranslations(IReadOnlyDictionary<string, ProjectTranslationContent> translations)
    {
        ArgumentNullException.ThrowIfNull(translations);

        _translations.RemoveAll(t => !translations.ContainsKey(t.Locale));
        foreach (var (locale, content) in translations)
        {
            SetTranslation(locale, content.Title, content.Summary, content.Description);
        }
    }

    /// <summary>Refuses a negative sort order.</summary>
    /// <param name="sortOrder">The display order to check.</param>
    /// <exception cref="DomainRuleException">The sort order is negative.</exception>
    private static void EnsureSortOrder(int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new DomainRuleException(ProjectErrors.SortOrderNegative, "A sort order cannot be negative.");
        }
    }

    /// <summary>Refuses input that breaks an invariant shared by creation and update.</summary>
    /// <param name="slug">The URL slug to check.</param>
    /// <param name="sortOrder">The display order to check.</param>
    /// <exception cref="DomainRuleException">The slug is empty or the sort order is negative.</exception>
    private static void EnsureValid(string slug, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new DomainRuleException(ProjectErrors.SlugRequired, "A project needs a slug.");
        }

        EnsureSortOrder(sortOrder);
    }
}
