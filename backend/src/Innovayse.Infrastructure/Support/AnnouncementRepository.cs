namespace Innovayse.Infrastructure.Support;

using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IAnnouncementRepository"/>.</summary>
/// <param name="db">The application database context.</param>
public sealed class AnnouncementRepository(AppDbContext db) : IAnnouncementRepository
{
    /// <inheritdoc/>
    public async Task<Announcement?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.Announcements.FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <inheritdoc/>
    public void Add(Announcement announcement) => db.Announcements.Add(announcement);

    /// <inheritdoc/>
    public void Remove(Announcement announcement) => db.Announcements.Remove(announcement);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Announcement> Items, int TotalCount)> ListAsync(
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.Announcements.OrderByDescending(a => a.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
