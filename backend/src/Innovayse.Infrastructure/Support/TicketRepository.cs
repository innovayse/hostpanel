namespace Innovayse.Infrastructure.Support;

using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="ITicketRepository"/>.</summary>
/// <param name="db">The application database context.</param>
public sealed class TicketRepository(AppDbContext db) : ITicketRepository
{
    /// <inheritdoc/>
    public async Task<Ticket?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.Tickets
            .Include(t => t.Replies)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    /// <inheritdoc/>
    public void Add(Ticket ticket) => db.Tickets.Add(ticket);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Ticket>> ListAsync(int page, int pageSize, CancellationToken ct) =>
        await db.Tickets
            .Include(t => t.Replies)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Ticket> Items, int TotalCount)> ListAsync(
        int page, int pageSize, TicketStatus? status, string? search, bool flaggedOnly, CancellationToken ct)
    {
        var query = db.Tickets.Include(t => t.Replies).AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        if (flaggedOnly)
        {
            query = query.Where(t => t.IsFlagged);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t => EF.Functions.ILike(t.Subject, $"%{search}%"));
        }

        query = query.OrderByDescending(t => t.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Ticket>> ListByDateRangeAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        await db.Tickets
            .Include(t => t.Replies)
            .Where(t => t.CreatedAt >= from && t.CreatedAt < to)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Ticket>> ListByClientIdAsync(int clientId, CancellationToken ct) =>
        await db.Tickets
            .Include(t => t.Replies)
            .Where(t => t.ClientId == clientId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<int> CountByStatusAsync(TicketStatus status, CancellationToken ct) =>
        await db.Tickets.CountAsync(t => t.Status == status, ct);

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Ticket> Items, int TotalCount)> ListByClientIdAsync(
        int clientId, int page, int pageSize, string? search, CancellationToken ct)
    {
        var query = db.Tickets
            .Include(t => t.Replies)
            .Where(t => t.ClientId == clientId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t => EF.Functions.ILike(t.Subject, $"%{search}%"));
        }

        query = query.OrderByDescending(t => t.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    /// <inheritdoc />
    public void Remove(Ticket ticket) => db.Tickets.Remove(ticket);

    /// <inheritdoc />
    public async Task<int> CountAsync(CancellationToken ct) =>
        await db.Tickets.CountAsync(ct);

    /// <inheritdoc />
    public async Task<int> CountByClientIdAndDateRangeAsync(
        int clientId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        await db.Tickets.CountAsync(t => t.ClientId == clientId && t.CreatedAt >= from && t.CreatedAt < to, ct);
}
