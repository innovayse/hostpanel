namespace Innovayse.Application.Support.Commands.PublishKbArticle;

/// <summary>Command to publish or unpublish a knowledge base article.</summary>
/// <param name="Id">The article primary key.</param>
/// <param name="Publish"><see langword="true"/> to publish; <see langword="false"/> to unpublish.</param>
public record PublishKbArticleCommand(int Id, bool Publish);
