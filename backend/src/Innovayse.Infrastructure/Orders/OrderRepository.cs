namespace Innovayse.Infrastructure.Orders;

using Innovayse.Domain.Orders;
using Innovayse.Domain.Orders.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IOrderRepository"/>.</summary>
public sealed class OrderRepository(AppDbContext db) : IOrderRepository
{
    /// <inheritdoc/>
    public async Task<Order?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    /// <inheritdoc/>
    public void Add(Order order) => db.Orders.Add(order);

    /// <inheritdoc/>
    public void Remove(Order order) => db.Orders.Remove(order);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> ListAsync(
        int page, int pageSize, OrderStatus? statusFilter, CancellationToken ct)
    {
        var query = db.Orders.AsQueryable();

        if (statusFilter.HasValue)
        {
            query = query.Where(o => o.Status == statusFilter.Value);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(o => o.Items)
            .ToListAsync(ct);

        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<int> GetNextOrderNumberAsync(CancellationToken ct) =>
        await db.Orders.AnyAsync(ct)
            ? await db.Orders.MaxAsync(o => o.Id, ct) + 1
            : 1;

    /// <inheritdoc/>
    public async Task<Order?> FindByOrderNumberAsync(string orderNumber, CancellationToken ct) =>
        await db.Orders.FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);
}
