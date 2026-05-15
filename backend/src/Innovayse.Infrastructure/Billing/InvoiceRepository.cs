namespace Innovayse.Infrastructure.Billing;

using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IInvoiceRepository"/>.</summary>
/// <param name="db">The application database context.</param>
public sealed class InvoiceRepository(AppDbContext db) : IInvoiceRepository
{
    /// <inheritdoc/>
    public async Task<Invoice?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.Invoices
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Invoice> Items, int TotalCount)> ListAsync(
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.Invoices.OrderByDescending(x => x.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Invoice>> ListByClientAsync(int clientId, CancellationToken ct) =>
        await db.Invoices
            .Include(x => x.Items)
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public void Add(Invoice invoice) => db.Invoices.Add(invoice);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Invoice>> GetPaidBetweenAsync(
        DateTimeOffset start, DateTimeOffset end, CancellationToken ct) =>
        await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Paid && i.PaidAt >= start && i.PaidAt <= end)
            .OrderBy(i => i.PaidAt)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken ct) =>
        await db.Invoices.ToListAsync(ct);
}
