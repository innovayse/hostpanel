namespace Innovayse.Application.Support.Queries.GetPublishedKbArticle;

/// <summary>Query to retrieve a knowledge base article that is published.</summary>
/// <remarks>
/// The public knowledge base route used to dispatch <c>GetKbArticleQuery</c>, which reads a row
/// by id and never looks at its published state -- so an unpublished draft was one guessed id
/// away from any anonymous visitor, even though the list beside it showed published rows only.
/// This message is the client-facing half of that split; <c>GetKbArticleQuery</c> stays as the
/// admin read that may return a draft.
/// </remarks>
/// <param name="Id">The article primary key.</param>
public record GetPublishedKbArticleQuery(int Id);
