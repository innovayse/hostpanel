namespace Innovayse.Application.Support.Commands.UpdateKbArticle;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Updates an existing knowledge base article's title, content, and category.
/// </summary>
public sealed class UpdateKbArticleHandler(IKbArticleRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="UpdateKbArticleCommand"/>.
    /// </summary>
    /// <param name="cmd">The update KB article command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the article is not found.</exception>
    public async Task HandleAsync(UpdateKbArticleCommand cmd, CancellationToken ct)
    {
        var article = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"KbArticle {cmd.Id} not found.");

        article.Update(cmd.Title, cmd.Content, cmd.Category);
        repo.Update(article);
        await uow.SaveChangesAsync(ct);
    }
}
