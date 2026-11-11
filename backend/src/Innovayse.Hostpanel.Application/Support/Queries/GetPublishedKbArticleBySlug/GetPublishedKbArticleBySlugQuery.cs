namespace Innovayse.Hostpanel.Application.Support.Queries.GetPublishedKbArticleBySlug;

/// <summary>Query to retrieve a published knowledge base article by its slug.</summary>
/// <remarks>
/// The slug is matched without regard to case, so a link typed with capitals still finds the article; the answer
/// always carries the canonical lower case slug. An unpublished article is not found, as with
/// <c>GetPublishedKbArticleQuery</c>.
/// </remarks>
/// <param name="Slug">The article's slug, in any letter case.</param>
public sealed record GetPublishedKbArticleBySlugQuery(string Slug);
