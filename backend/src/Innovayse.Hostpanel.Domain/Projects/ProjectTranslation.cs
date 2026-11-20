using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Projects;

/// <summary>
/// A locale-specific translation for a <see cref="Project"/>.
/// Stored in the <c>ProjectTranslations</c> table.
/// </summary>
public sealed class ProjectTranslation : BaseEntity
{
    /// <summary>Longest a locale code may be: matches the <c>ProjectTranslations.Locale</c> column.</summary>
    public const int LocaleMaxLength = 10;

    /// <summary>Longest Title may be: matches the <c>ProjectTranslations.Title</c> column.</summary>
    public const int TitleMaxLength = 200;

    /// <summary>Longest Summary may be: matches the <c>ProjectTranslations.Summary</c> column.</summary>
    public const int SummaryMaxLength = 500;

    /// <summary>Longest Description may be: matches the <c>ProjectTranslations.Description</c> column.</summary>
    public const int DescriptionMaxLength = 4000;

    /// <summary>Gets the FK to the parent <see cref="Project"/>.</summary>
    public int ProjectId { get; private set; }

    /// <summary>Gets the BCP-47 locale code (e.g. "en", "ru", "hy", "de", "es").</summary>
    public string Locale { get; private set; } = string.Empty;

    /// <summary>Gets the project's title in this locale.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Gets a one-line summary in this locale, or <c>null</c>.</summary>
    public string? Summary { get; private set; }

    /// <summary>Gets the long description in this locale, or <c>null</c>.</summary>
    public string? Description { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private ProjectTranslation() { }

    /// <summary>
    /// Creates a new project translation.
    /// </summary>
    /// <param name="locale">BCP-47 locale code.</param>
    /// <param name="title">The project's title in this locale.</param>
    /// <param name="summary">A one-line summary in this locale, or <c>null</c></param>
    /// <param name="description">The long description in this locale, or <c>null</c></param>
    /// <returns>A new <see cref="ProjectTranslation"/>.</returns>
    internal static ProjectTranslation Create(string locale, string title, string? summary, string? description)
    {
        return new ProjectTranslation
        {
            Locale = locale,
            Title = title,
            Summary = summary,
            Description = description
        };
    }

    /// <summary>
    /// Updates every translatable field.
    /// </summary>
    /// <param name="title">The project's title in this locale.</param>
    /// <param name="summary">A one-line summary in this locale, or <c>null</c></param>
    /// <param name="description">The long description in this locale, or <c>null</c></param>
    internal void Update(string title, string? summary, string? description)
    {
        Title = title;
        Summary = summary;
        Description = description;
    }
}
