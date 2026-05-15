namespace Innovayse.Application.Support.Commands.DeleteKbArticle;

/// <summary>Command to permanently delete a knowledge base article.</summary>
/// <param name="Id">The article primary key.</param>
public record DeleteKbArticleCommand(int Id);
