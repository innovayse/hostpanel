namespace Innovayse.Infrastructure.Support;

using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IPredefinedReplyRepository"/>.</summary>
/// <param name="db">The application database context.</param>
public sealed class PredefinedReplyRepository(AppDbContext db) : IPredefinedReplyRepository
{
    /// <inheritdoc/>
    public async Task<PredefinedReplyCategory?> FindCategoryByIdAsync(int id, CancellationToken ct) =>
        await db.PredefinedReplyCategories.FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc/>
    public void AddCategory(PredefinedReplyCategory category) => db.PredefinedReplyCategories.Add(category);

    /// <inheritdoc/>
    public void RemoveCategory(PredefinedReplyCategory category) => db.PredefinedReplyCategories.Remove(category);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<(PredefinedReplyCategory Category, int ReplyCount)>> ListCategoriesAsync(
        CancellationToken ct)
    {
        var categories = await db.PredefinedReplyCategories
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                Category = c,
                ReplyCount = db.PredefinedReplies.Count(r => r.CategoryId == c.Id),
            })
            .ToListAsync(ct);

        return categories.Select(x => (x.Category, x.ReplyCount)).ToList();
    }

    /// <inheritdoc/>
    public async Task<PredefinedReply?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.PredefinedReplies.FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc/>
    public void Add(PredefinedReply reply) => db.PredefinedReplies.Add(reply);

    /// <inheritdoc/>
    public void Remove(PredefinedReply reply) => db.PredefinedReplies.Remove(reply);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<(PredefinedReply Reply, string? CategoryName)>> ListAsync(
        int? categoryId, CancellationToken ct)
    {
        var query = db.PredefinedReplies.AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(r => r.CategoryId == categoryId.Value);
        }

        var results = await query
            .OrderBy(r => r.Name)
            .Join(
                db.PredefinedReplyCategories,
                r => r.CategoryId,
                c => c.Id,
                (r, c) => new { Reply = r, CategoryName = c.Name })
            .ToListAsync(ct);

        return results.Select(x => (x.Reply, (string?)x.CategoryName)).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<(PredefinedReply Reply, string? CategoryName)>> SearchAsync(
        string query, CancellationToken ct)
    {
        var results = await db.PredefinedReplies
            .Where(r => EF.Functions.ILike(r.Name, $"%{query}%") ||
                         EF.Functions.ILike(r.Content, $"%{query}%"))
            .OrderBy(r => r.Name)
            .Join(
                db.PredefinedReplyCategories,
                r => r.CategoryId,
                c => c.Id,
                (r, c) => new { Reply = r, CategoryName = c.Name })
            .ToListAsync(ct);

        return results.Select(x => (x.Reply, (string?)x.CategoryName)).ToList();
    }
}
