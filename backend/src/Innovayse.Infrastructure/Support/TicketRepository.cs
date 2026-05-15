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
    public async Task<IReadOnlyList<Ticket>> ListByClientIdAsync(int clientId, CancellationToken ct) =>
        await db.Tickets
            .Include(t => t.Replies)
            .Where(t => t.ClientId == clientId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<int> CountByStatusAsync(TicketStatus status, CancellationToken ct) =>
        await db.Tickets.CountAsync(t => t.Status == status, ct);
}
