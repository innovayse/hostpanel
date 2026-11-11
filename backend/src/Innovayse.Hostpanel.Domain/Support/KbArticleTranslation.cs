using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Support;

/// <summary>
/// The title and body of a <see cref="KbArticle"/> in one locale. Owned by the article: it is created, changed
/// and removed only through the article's translation methods.
/// </summary>
public sealed class KbArticleTranslation : BaseEntity
{
    /// <summary>Longest a locale code may be: matches the <c>KbArticleTranslations.Locale</c> column.</summary>
    public const int LocaleMaxLength = 10;

    /// <summary>Longest an article title may be: matches the <c>KbArticleTranslations.Title</c> column.</summary>
    public const int TitleMaxLength = 500;

    /// <summary>Gets the id of the article this text belongs to.</summary>
    public int ArticleId { get; private set; }

    /// <summary>Gets the locale code of this text, for example <c>en</c>.</summary>
    public string Locale { get; private set; } = string.Empty;

    /// <summary>Gets the article title in this locale.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Gets the article body in this locale.</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private KbArticleTranslation() { }

    /// <summary>Creates the text of one locale.</summary>
    /// <param name="locale">The locale code.</param>
    /// <param name="title">The title in that locale.</param>
    /// <param name="content">The body in that locale.</param>
    /// <returns>The new translation, not yet attached to an article row.</returns>
    /// <exception cref="ArgumentException">Thrown when any argument is null or whitespace.</exception>
    internal static KbArticleTranslation Create(string locale, string title, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        return new KbArticleTranslation { Locale = locale, Title = title, Content = content };
    }

    /// <summary>Replaces the text of this locale.</summary>
    /// <param name="title">The new title.</param>
    /// <param name="content">The new body.</param>
    /// <exception cref="ArgumentException">Thrown when any argument is null or whitespace.</exception>
    internal void Update(string title, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        Title = title;
        Content = content;
    }
}
