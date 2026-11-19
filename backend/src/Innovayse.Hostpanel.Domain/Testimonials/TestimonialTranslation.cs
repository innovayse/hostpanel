using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Testimonials;

/// <summary>
/// A locale-specific translation for a <see cref="Testimonial"/>.
/// Stored in the <c>TestimonialTranslations</c> table.
/// </summary>
public sealed class TestimonialTranslation : BaseEntity
{
    /// <summary>Longest a locale code may be: matches the <c>TestimonialTranslations.Locale</c> column.</summary>
    public const int LocaleMaxLength = 10;

    /// <summary>Longest Role may be: matches the <c>TestimonialTranslations.Role</c> column.</summary>
    public const int RoleMaxLength = 200;

    /// <summary>Longest Quote may be: matches the <c>TestimonialTranslations.Quote</c> column.</summary>
    public const int QuoteMaxLength = 2000;

    /// <summary>Gets the FK to the parent <see cref="Testimonial"/>.</summary>
    public int TestimonialId { get; private set; }

    /// <summary>Gets the BCP-47 locale code (e.g. "en", "ru", "hy", "de", "es").</summary>
    public string Locale { get; private set; } = string.Empty;

    /// <summary>Gets the client's job title in this locale, or <c>null</c>.</summary>
    public string? Role { get; private set; }

    /// <summary>Gets the testimonial text in this locale.</summary>
    public string Quote { get; private set; } = string.Empty;

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private TestimonialTranslation() { }

    /// <summary>
    /// Creates a new testimonial translation.
    /// </summary>
    /// <param name="locale">BCP-47 locale code.</param>
    /// <param name="role">The client's job title in this locale, or <c>null</c></param>
    /// <param name="quote">The testimonial text in this locale.</param>
    /// <returns>A new <see cref="TestimonialTranslation"/>.</returns>
    internal static TestimonialTranslation Create(string locale, string? role, string quote)
    {
        return new TestimonialTranslation
        {
            Locale = locale,
            Role = role,
            Quote = quote
        };
    }

    /// <summary>
    /// Updates every translatable field.
    /// </summary>
    /// <param name="role">The client's job title in this locale, or <c>null</c></param>
    /// <param name="quote">The testimonial text in this locale.</param>
    internal void Update(string? role, string quote)
    {
        Role = role;
        Quote = quote;
    }
}
