using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Partners;

/// <summary>
/// A locale-specific translation for a <see cref="Partner"/>.
/// Stored in the <c>PartnerTranslations</c> table.
/// </summary>
public sealed class PartnerTranslation : BaseEntity
{
    /// <summary>Longest a locale code may be: matches the <c>PartnerTranslations.Locale</c> column.</summary>
    public const int LocaleMaxLength = 10;

    /// <summary>Longest Name may be: matches the <c>PartnerTranslations.Name</c> column.</summary>
    public const int NameMaxLength = 200;

    /// <summary>Gets the FK to the parent <see cref="Partner"/>.</summary>
    public int PartnerId { get; private set; }

    /// <summary>Gets the BCP-47 locale code (e.g. "en", "ru", "hy", "de", "es").</summary>
    public string Locale { get; private set; } = string.Empty;

    /// <summary>Gets the partner's name in this locale.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private PartnerTranslation() { }

    /// <summary>
    /// Creates a new partner translation.
    /// </summary>
    /// <param name="locale">BCP-47 locale code.</param>
    /// <param name="name">The partner's name in this locale.</param>
    /// <returns>A new <see cref="PartnerTranslation"/>.</returns>
    internal static PartnerTranslation Create(string locale, string name)
    {
        return new PartnerTranslation
        {
            Locale = locale,
            Name = name
        };
    }

    /// <summary>
    /// Updates every translatable field.
    /// </summary>
    /// <param name="name">The partner's name in this locale.</param>
    internal void Update(string name)
    {
        Name = name;
    }
}
