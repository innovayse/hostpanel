namespace Innovayse.Application.Support.Commands.UpdateKbArticle;

/// <summary>Command to update an existing knowledge base article.</summary>
/// <param name="Id">The article primary key.</param>
/// <param name="Title">The new article title.</param>
/// <param name="Content">The new article body content.</param>
/// <param name="Category">The new category name.</param>
public record UpdateKbArticleCommand(int Id, string Title, string Content, string Category);
