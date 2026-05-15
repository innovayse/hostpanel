namespace Innovayse.Application.Support.Commands.CreateKbArticle;

/// <summary>Command to create a new knowledge base article.</summary>
/// <param name="Title">The article title.</param>
/// <param name="Content">The full article body content.</param>
/// <param name="Category">The category name for the article.</param>
public record CreateKbArticleCommand(string Title, string Content, string Category);
