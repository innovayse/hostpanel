namespace Innovayse.Infrastructure.Support;

using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="INetworkIssueRepository"/>.</summary>
/// <param name="db">The application database context.</param>
public sealed class NetworkIssueRepository(AppDbContext db) : INetworkIssueRepository
{
    /// <inheritdoc/>
    public async Task<NetworkIssue?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.NetworkIssues.FirstOrDefaultAsync(n => n.Id == id, ct);

    /// <inheritdoc/>
    public void Add(NetworkIssue issue) => db.NetworkIssues.Add(issue);

    /// <inheritdoc/>
    public void Remove(NetworkIssue issue) => db.NetworkIssues.Remove(issue);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<NetworkIssue> Items, int TotalCount)> ListAsync(
        int page, int pageSize, NetworkIssueStatus? status, CancellationToken ct)
    {
        var query = db.NetworkIssues.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(n => n.Status == status.Value);
        }

        query = query.OrderByDescending(n => n.StartDate);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
