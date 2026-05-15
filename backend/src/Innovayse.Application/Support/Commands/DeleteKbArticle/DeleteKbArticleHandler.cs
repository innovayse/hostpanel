namespace Innovayse.Application.Support.Commands.DeleteKbArticle;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Permanently deletes a knowledge base article from persistence.
/// </summary>
public sealed class DeleteKbArticleHandler(IKbArticleRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="DeleteKbArticleCommand"/>.
    /// </summary>
    /// <param name="cmd">The delete KB article command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the article is not found.</exception>
    public async Task HandleAsync(DeleteKbArticleCommand cmd, CancellationToken ct)
    {
        var article = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"KbArticle {cmd.Id} not found.");

        repo.Delete(article);
        await uow.SaveChangesAsync(ct);
    }
}
