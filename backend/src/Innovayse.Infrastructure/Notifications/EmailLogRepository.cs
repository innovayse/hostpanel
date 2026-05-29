namespace Innovayse.Infrastructure.Notifications;

using Innovayse.Domain.Clients;
using Innovayse.Domain.Notifications;
using Innovayse.Domain.Notifications.Interfaces;
using Innovayse.Infrastructure.Auth;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IEmailLogRepository"/>.</summary>
/// <param name="db">The application database context.</param>
public sealed class EmailLogRepository(AppDbContext db) : IEmailLogRepository
{
    /// <inheritdoc/>
    public void Add(EmailLog log) => db.EmailLogs.Add(log);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EmailLog>> ListAsync(int page, int pageSize, CancellationToken ct) =>
        await db.EmailLogs
            .OrderByDescending(l => l.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<EmailLog> Items, int TotalCount)> ListByClientIdAsync(
        int clientId, int page, int pageSize, CancellationToken ct)
    {
        var clientEmail = await db.Clients
            .Where(c => c.Id == clientId)
            .Join(db.Users, c => c.UserId, u => u.Id, (c, u) => u.Email)
            .FirstOrDefaultAsync(ct);

        if (clientEmail is null)
        {
            return ([], 0);
        }

        var query = db.EmailLogs.Where(l => l.To == clientEmail);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
