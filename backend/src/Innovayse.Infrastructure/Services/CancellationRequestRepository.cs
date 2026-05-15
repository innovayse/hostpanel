namespace Innovayse.Infrastructure.Services;

using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="ICancellationRequestRepository"/>.</summary>
public sealed class CancellationRequestRepository(AppDbContext db) : ICancellationRequestRepository
{
    /// <inheritdoc/>
    public async Task<(IReadOnlyList<CancellationRequest> Items, int TotalCount)> ListAsync(
        int page, int pageSize, CancellationToken ct)
    {
        var total = await db.CancellationRequests.CountAsync(ct);
        var items = await db.CancellationRequests
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<CancellationRequest?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.CancellationRequests.FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc/>
    public async Task<CancellationRequest?> FindByServiceIdAsync(int serviceId, CancellationToken ct) =>
        await db.CancellationRequests.FirstOrDefaultAsync(r => r.ServiceId == serviceId, ct);

    /// <inheritdoc/>
    public void Add(CancellationRequest request) => db.CancellationRequests.Add(request);

    /// <inheritdoc/>
    public void Remove(CancellationRequest request) => db.CancellationRequests.Remove(request);
}
