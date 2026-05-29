namespace Innovayse.Infrastructure.Billing;

using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IBillableItemRepository"/>.</summary>
/// <param name="db">The application database context.</param>
public sealed class BillableItemRepository(AppDbContext db) : IBillableItemRepository
{
    /// <inheritdoc/>
    public async Task<BillableItem?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.BillableItems.FirstOrDefaultAsync(x => x.Id == id, ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<BillableItem> Items, int TotalCount)> ListAsync(
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.BillableItems.OrderByDescending(x => x.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BillableItem>> ListUninvoicedAsync(int clientId, CancellationToken ct) =>
        await db.BillableItems
            .Where(x => x.ClientId == clientId && !x.IsInvoiced)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BillableItem>> ListRecurringAsync(int clientId, CancellationToken ct) =>
        await db.BillableItems
            .Where(x => x.ClientId == clientId && x.Type == BillableItemType.Recurring)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BillableItem>> GetDueForCronInvoicingAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return await db.BillableItems
            .Where(x => !x.IsInvoiced && x.Type == BillableItemType.OneTime && x.NextDueDate <= now)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BillableItem>> GetDueForRecurrenceAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return await db.BillableItems
            .Where(x => x.Type == BillableItemType.Recurring && x.NextDueDate != null && x.NextDueDate <= now)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BillableItem>> FindByIdsAsync(IReadOnlyList<int> ids, CancellationToken ct) =>
        await db.BillableItems.Where(x => ids.Contains(x.Id)).ToListAsync(ct);

    /// <inheritdoc/>
    public void AddRange(IEnumerable<BillableItem> items) => db.BillableItems.AddRange(items);

    /// <inheritdoc/>
    public void Add(BillableItem item) => db.BillableItems.Add(item);

    /// <inheritdoc/>
    public void Delete(BillableItem item) => db.BillableItems.Remove(item);
}
