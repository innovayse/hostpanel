using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Locations;

/// <summary>
/// A locale-specific translation for a <see cref="ServerLocation"/>.
/// Stored in the <c>ServerLocationTranslations</c> table.
/// </summary>
public sealed class ServerLocationTranslation : BaseEntity
{
    /// <summary>Longest a locale code may be: matches the <c>ServerLocationTranslations.Locale</c> column.</summary>
    public const int LocaleMaxLength = 10;

    /// <summary>Longest Name may be: matches the <c>ServerLocationTranslations.Name</c> column.</summary>
    public const int NameMaxLength = 120;

    /// <summary>Longest City may be: matches the <c>ServerLocationTranslations.City</c> column.</summary>
    public const int CityMaxLength = 120;

    /// <summary>Longest LatencyNote may be: matches the <c>ServerLocationTranslations.LatencyNote</c> column.</summary>
    public const int LatencyNoteMaxLength = 200;

    /// <summary>Gets the FK to the parent <see cref="ServerLocation"/>.</summary>
    public int ServerLocationId { get; private set; }

    /// <summary>Gets the BCP-47 locale code (e.g. "en", "ru", "hy", "de", "es").</summary>
    public string Locale { get; private set; } = string.Empty;

    /// <summary>Gets the data-centre name in this locale.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the city in this locale, or <c>null</c>.</summary>
    public string? City { get; private set; }

    /// <summary>Gets the short latency note in this locale, or <c>null</c>.</summary>
    public string? LatencyNote { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private ServerLocationTranslation() { }

    /// <summary>
    /// Creates a new location translation.
    /// </summary>
    /// <param name="locale">BCP-47 locale code.</param>
    /// <param name="name">The data-centre name in this locale.</param>
    /// <param name="city">The city in this locale, or <c>null</c>.</param>
    /// <param name="latencyNote">A short latency note in this locale, or <c>null</c>.</param>
    /// <returns>A new <see cref="ServerLocationTranslation"/>.</returns>
    internal static ServerLocationTranslation Create(string locale, string name, string? city, string? latencyNote)
    {
        return new ServerLocationTranslation
        {
            Locale = locale,
            Name = name,
            City = city,
            LatencyNote = latencyNote
        };
    }

    /// <summary>
    /// Updates every translatable field.
    /// </summary>
    /// <param name="name">The data-centre name in this locale.</param>
    /// <param name="city">The city in this locale, or <c>null</c>.</param>
    /// <param name="latencyNote">A short latency note in this locale, or <c>null</c>.</param>
    internal void Update(string name, string? city, string? latencyNote)
    {
        Name = name;
        City = city;
        LatencyNote = latencyNote;
    }
}
