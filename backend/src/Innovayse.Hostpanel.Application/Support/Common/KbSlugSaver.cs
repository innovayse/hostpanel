using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Support;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Support.Common;

/// <summary>
/// Saves a new knowledge base article or category with a slug nobody else has, and tries again when another request
/// takes the slug between <see cref="KbSlugAllocator"/> picking it and the save. This is the one place that retry
/// lives: the create use cases and the importer all go through it.
/// </summary>
/// <remarks>
/// The allocator only reads. Two requests can therefore pick the same free slug at the same moment; the unique index
/// refuses the second save with a <see cref="DbUpdateException"/>. This helper recognises that case, because the slug it
/// tried is now stored, and allocates again. A refusal for any other reason, or one that repeats on the last attempt, is
/// not hidden: it is rethrown.
/// </remarks>
public sealed class KbSlugSaver
{
    /// <summary>How many times the save is tried before the refusal is rethrown.</summary>
    public const int MaxAttempts = 3;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Picks a free slug for each attempt.</summary>
    private readonly KbSlugAllocator _slugs;

    /// <summary>Initializes the helper.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="slugs">Picks a free slug for each attempt.</param>
    public KbSlugSaver(IHostpanelDbContext context, KbSlugAllocator slugs)
    {
        _context = context;
        _slugs = slugs;
    }

    /// <summary>Saves a new article under a slug built from its title.</summary>
    /// <param name="title">The title the slug is built from.</param>
    /// <param name="create">Builds the article around the slug it is given.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The saved article.</returns>
    public Task<KbArticle> SaveArticleAsync(string title, Func<string, KbArticle> create, CancellationToken ct) =>
        SaveAsync(
            token => _slugs.AllocateForArticleAsync(title, token),
            create,
            article => _context.KbArticles.Add(article),
            article => _context.KbArticles.Remove(article),
            (slug, token) => _context.KbArticles.AsNoTracking().AnyAsync(a => a.Slug == slug, token),
            ct);

    /// <summary>Saves a new category under a slug built from its name.</summary>
    /// <param name="name">The name the slug is built from.</param>
    /// <param name="create">Builds the category around the slug it is given.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The saved category.</returns>
    public Task<KbCategory> SaveCategoryAsync(string name, Func<string, KbCategory> create, CancellationToken ct) =>
        SaveAsync(
            token => _slugs.AllocateForCategoryAsync(name, token),
            create,
            category => _context.KbCategories.Add(category),
            category => _context.KbCategories.Remove(category),
            (slug, token) => _context.KbCategories.AsNoTracking().AnyAsync(c => c.Slug == slug, token),
            ct);

    /// <summary>The retry loop both kinds share.</summary>
    /// <typeparam name="TEntity">The kind of entity saved.</typeparam>
    /// <param name="allocate">Picks a slug that is free now.</param>
    /// <param name="create">Builds the entity around the slug.</param>
    /// <param name="add">Starts tracking the new entity.</param>
    /// <param name="discard">Stops tracking an entity whose save was refused.</param>
    /// <param name="isTaken">Whether a stored row already has the slug.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The saved entity.</returns>
    /// <exception cref="DbUpdateException">
    /// Thrown when the save is refused for a reason other than the slug being taken, or when the slug is taken on every attempt.
    /// </exception>
    public async Task<TEntity> SaveAsync<TEntity>(
        Func<CancellationToken, Task<string>> allocate,
        Func<string, TEntity> create,
        Action<TEntity> add,
        Action<TEntity> discard,
        Func<string, CancellationToken, Task<bool>> isTaken,
        CancellationToken ct)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(allocate);
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(add);
        ArgumentNullException.ThrowIfNull(discard);
        ArgumentNullException.ThrowIfNull(isTaken);

        for (var attempt = 1; ; attempt++)
        {
            var slug = await allocate(ct);
            var entity = create(slug);
            add(entity);
            try
            {
                await _context.SaveChangesAsync(ct);
                return entity;
            }
            catch (DbUpdateException)
            {
                // Another request may have stored the slug between the allocation and this save: allocate again.
                // Any other refusal, or the last attempt, is not ours to hide.
                if (attempt >= MaxAttempts || !await isTaken(slug, ct))
                {
                    throw;
                }

                discard(entity);
            }
        }
    }
}
