namespace Innovayse.Infrastructure.Support;

using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IKbArticleRepository"/>.</summary>
/// <param name="db">The application database context.</param>
public sealed class KbArticleRepository(AppDbContext db) : IKbArticleRepository
{
    /// <inheritdoc/>
    public async Task<KbArticle?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.KbArticles.FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <inheritdoc/>
    public void Add(KbArticle article) => db.KbArticles.Add(article);

    /// <inheritdoc/>
    public void Update(KbArticle article) => db.KbArticles.Update(article);

    /// <inheritdoc/>
    public void Delete(KbArticle article) => db.KbArticles.Remove(article);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<KbArticle>> ListAllAsync(CancellationToken ct) =>
        await db.KbArticles.OrderBy(a => a.Category).ThenBy(a => a.Title).ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<KbArticle>> ListPublishedAsync(CancellationToken ct) =>
        await db.KbArticles
            .Where(a => a.IsPublished)
            .OrderBy(a => a.Category)
            .ThenBy(a => a.Title)
            .ToListAsync(ct);
}
