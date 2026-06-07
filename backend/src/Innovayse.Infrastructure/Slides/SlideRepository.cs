namespace Innovayse.Infrastructure.Slides;

using Innovayse.Domain.Slides;
using Innovayse.Domain.Slides.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="ISlideRepository"/>.</summary>
public sealed class SlideRepository(AppDbContext db) : ISlideRepository
{
    /// <inheritdoc/>
    public async Task<Slide?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.Slides
            .Include(s => s.Translations)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <inheritdoc/>
    public async Task<List<Slide>> ListAllAsync(CancellationToken ct) =>
        await db.Slides
            .Include(s => s.Translations)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<List<Slide>> ListActiveAsync(DateTimeOffset now, CancellationToken ct) =>
        await db.Slides
            .Include(s => s.Translations)
            .Where(s => s.IsActive
                && (s.VisibleFrom == null || s.VisibleFrom <= now)
                && (s.VisibleUntil == null || s.VisibleUntil >= now))
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public void Add(Slide slide) => db.Slides.Add(slide);

    /// <inheritdoc/>
    public void Remove(Slide slide) => db.Slides.Remove(slide);
}
