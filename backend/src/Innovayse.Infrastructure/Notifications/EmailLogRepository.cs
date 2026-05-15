namespace Innovayse.Infrastructure.Notifications;

using Innovayse.Domain.Notifications;
using Innovayse.Domain.Notifications.Interfaces;
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
}
