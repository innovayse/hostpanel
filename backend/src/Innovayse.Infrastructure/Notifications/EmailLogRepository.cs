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
    public async Task<EmailLog?> FindByClientIdAsync(int clientId, int emailLogId, CancellationToken ct)
    {
        var clientEmail = await ClientEmailAsync(clientId, ct);
        if (clientEmail is null) return null;

        // Both conditions in one query: the address is what ties a log entry to an account, so
        // asking for "this id, addressed to this client" is the whole ownership check.
        return await db.EmailLogs
            .FirstOrDefaultAsync(l => l.Id == emailLogId && l.To == clientEmail, ct);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<EmailLog> Items, int TotalCount)> ListByClientIdAsync(
        int clientId, int page, int pageSize, CancellationToken ct)
    {
        var clientEmail = await ClientEmailAsync(clientId, ct);

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

    /// <summary>The address a client's mail is sent to.</summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The address, or null when no such client exists.</returns>
    private async Task<string?> ClientEmailAsync(int clientId, CancellationToken ct) =>
        await db.Clients
            .Where(c => c.Id == clientId)
            .Join(db.Users, c => c.UserId, u => u.Id, (c, u) => u.Email)
            .FirstOrDefaultAsync(ct);
}
