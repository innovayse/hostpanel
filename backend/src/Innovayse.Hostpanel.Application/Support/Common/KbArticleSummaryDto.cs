namespace Innovayse.Hostpanel.Application.Support.Common;

/// <summary>A published article in a list: its title and a short plain text excerpt, in the reader's language.</summary>
/// <param name="Id">The article id.</param>
/// <param name="Title">The title in the reader's language.</param>
/// <param name="Excerpt">The first words of the body as plain text.</param>
/// <param name="Category">The name of the article's category.</param>
/// <param name="Slug">The article's address segment.</param>
/// <param name="CategorySlug">The address segment of the article's category.</param>
public sealed record KbArticleSummaryDto(int Id, string Title, string Excerpt, string Category, string Slug, string CategorySlug);
