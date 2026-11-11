using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Support;

/// <summary>
/// Represents a knowledge base article that can be published and made visible to clients. Its title and body
/// live in per-locale <see cref="KbArticleTranslation"/> rows; the article itself holds what is the same in
/// every language: its category, its slug and whether it is published.
/// Created via <see cref="Create"/> factory: no public constructor.
/// </summary>
public sealed class KbArticle : BaseEntity
{
    /// <summary>The translations, kept in a field so only this class changes them.</summary>
    private readonly List<KbArticleTranslation> _translations = [];

    /// <summary>Gets the id of the <see cref="KbCategory"/> this article belongs to.</summary>
    public int CategoryId { get; private set; }

    /// <summary>
    /// Gets the article's address segment: unique without regard to case, fixed once the article is created so
    /// links to it keep working. See <see cref="KbSlugRules"/>.
    /// </summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether this article is visible to clients.</summary>
    public bool IsPublished { get; private set; }

    /// <summary>Gets the title and body of the article in each locale it is written in.</summary>
    public IReadOnlyList<KbArticleTranslation> Translations => _translations.AsReadOnly();

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private KbArticle() { }

    /// <summary>
    /// Creates a new unpublished <see cref="KbArticle"/> with no translations yet.
    /// </summary>
    /// <param name="categoryId">The id of the category the article belongs to.</param>
    /// <param name="slug">The article's slug, already unique: see <see cref="KbSlugRules.IsValidArticleSlug"/>.</param>
    /// <returns>A new unpublished <see cref="KbArticle"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the category id is not positive.</exception>
    /// <exception cref="ArgumentException">Thrown when the slug is not a valid article slug.</exception>
    public static KbArticle Create(int categoryId, string slug)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(categoryId);
        KbSlugRules.EnsureValidArticleSlug(slug);

        return new KbArticle { CategoryId = categoryId, Slug = slug, IsPublished = false };
    }

    /// <summary>Moves the article to another category.</summary>
    /// <param name="categoryId">The id of the new category.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the category id is not positive.</exception>
    public void MoveToCategory(int categoryId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(categoryId);
        CategoryId = categoryId;
    }

    /// <summary>Sets the text of one locale, adding it or replacing the existing one.</summary>
    /// <param name="locale">The locale code.</param>
    /// <param name="title">The title in that locale.</param>
    /// <param name="content">The body in that locale.</param>
    /// <exception cref="ArgumentException">Thrown when any argument is null or whitespace.</exception>
    public void SetTranslation(string locale, string title, string content)
    {
        var existing = _translations.FirstOrDefault(t => t.Locale == locale);
        if (existing is not null)
        {
            existing.Update(title, content);
        }
        else
        {
            _translations.Add(KbArticleTranslation.Create(locale, title, content));
        }
    }

    /// <summary>
    /// Makes the article's translations exactly the given set: a locale not in it is removed, the others are
    /// added or updated.
    /// </summary>
    /// <param name="translations">The text of each locale, keyed by locale code.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="translations"/> is null.</exception>
    public void ReplaceTranslations(IReadOnlyDictionary<string, KbArticleTranslationContent> translations)
    {
        ArgumentNullException.ThrowIfNull(translations);

        _translations.RemoveAll(t => !translations.ContainsKey(t.Locale));
        foreach (var (locale, content) in translations)
        {
            SetTranslation(locale, content.Title, content.Content);
        }
    }

    /// <summary>
    /// Publishes this article, making it visible to clients.
    /// </summary>
    public void Publish() => IsPublished = true;

    /// <summary>
    /// Unpublishes this article, hiding it from clients.
    /// </summary>
    public void Unpublish() => IsPublished = false;
}
