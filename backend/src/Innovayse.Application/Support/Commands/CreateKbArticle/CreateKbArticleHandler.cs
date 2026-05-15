namespace Innovayse.Application.Support.Commands.CreateKbArticle;

using Innovayse.Application.Common;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Creates a new knowledge base article and persists it via <see cref="IKbArticleRepository"/>.
/// </summary>
public sealed class CreateKbArticleHandler(IKbArticleRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="CreateKbArticleCommand"/>.
    /// </summary>
    /// <param name="cmd">The create KB article command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created article ID.</returns>
    /// <exception cref="ArgumentException">Propagated from domain when title, content, or category is null or whitespace.</exception>
    public async Task<int> HandleAsync(CreateKbArticleCommand cmd, CancellationToken ct)
    {
        var article = KbArticle.Create(cmd.Title, cmd.Content, cmd.Category);
        repo.Add(article);
        await uow.SaveChangesAsync(ct);
        return article.Id;
    }
}
