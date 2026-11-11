namespace Innovayse.Hostpanel.Application.Support.Common;

/// <summary>One locale of an article as read from the database, before one locale is chosen for the reader.</summary>
/// <param name="Locale">The locale code.</param>
/// <param name="Title">The title in that locale.</param>
/// <param name="Content">The body in that locale, or only its first characters when the row was read for a list.</param>
public sealed record KbTranslationRow(string Locale, string Title, string Content);
