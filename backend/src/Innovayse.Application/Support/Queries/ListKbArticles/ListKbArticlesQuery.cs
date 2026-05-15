namespace Innovayse.Application.Support.Queries.ListKbArticles;

/// <summary>Query to list knowledge base articles, optionally filtered to published only.</summary>
/// <param name="PublishedOnly">When <see langword="true"/>, returns only published articles.</param>
public record ListKbArticlesQuery(bool PublishedOnly);
