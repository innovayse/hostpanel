namespace Innovayse.Infrastructure.Audit;

using Innovayse.Domain.Audit;
using Innovayse.Domain.Audit.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IActivityLogRepository"/>.</summary>
/// <param name="db">The application database context.</param>
public sealed class ActivityLogRepository(AppDbContext db) : IActivityLogRepository
{
    /// <inheritdoc/>
    public void Add(ActivityLog log) => db.ActivityLogs.Add(log);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<ActivityLog> Items, int TotalCount)> ListByClientIdAsync(
        int clientId, int page, int pageSize,
        DateTimeOffset? date, string? adminSearch, string? description, string? ipAddress,
        CancellationToken ct)
    {
        var query = db.ActivityLogs.Where(l => l.ClientId == clientId);

        if (date.HasValue)
        {
            var day = date.Value.UtcDateTime.Date;
            var nextDay = day.AddDays(1);
            query = query.Where(l => l.CreatedAt >= day && l.CreatedAt < nextDay);
        }

        if (!string.IsNullOrWhiteSpace(adminSearch))
        {
            query = query.Where(l =>
                (l.AdminName != null && EF.Functions.ILike(l.AdminName, $"%{adminSearch}%")) ||
                (l.AdminEmail != null && EF.Functions.ILike(l.AdminEmail, $"%{adminSearch}%")));
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            query = query.Where(l => EF.Functions.ILike(l.Description, $"%{description}%"));
        }

        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            query = query.Where(l => l.IpAddress != null && EF.Functions.ILike(l.IpAddress, $"%{ipAddress}%"));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
