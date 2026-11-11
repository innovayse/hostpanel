namespace Innovayse.Hostpanel.Application.Support.Common;

/// <summary>A published article as a reader sees it, in the reader's language.</summary>
/// <param name="Id">The article id.</param>
/// <param name="Title">The title in the reader's language.</param>
/// <param name="Content">The body in the reader's language.</param>
/// <param name="Category">The name of the article's category.</param>
/// <param name="Slug">The article's address segment.</param>
/// <param name="CategorySlug">The address segment of the article's category.</param>
public sealed record PublishedKbArticleDto(int Id, string Title, string Content, string Category, string Slug, string CategorySlug);
