namespace Innovayse.Application.Support.Commands.PublishKbArticle;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Publishes or unpublishes a knowledge base article.
/// </summary>
public sealed class PublishKbArticleHandler(IKbArticleRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="PublishKbArticleCommand"/>.
    /// </summary>
    /// <param name="cmd">The publish/unpublish KB article command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the article is not found.</exception>
    public async Task HandleAsync(PublishKbArticleCommand cmd, CancellationToken ct)
    {
        var article = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"KbArticle {cmd.Id} not found.");

        if (cmd.Publish)
        {
            article.Publish();
        }
        else
        {
            article.Unpublish();
        }

        repo.Update(article);
        await uow.SaveChangesAsync(ct);
    }
}
