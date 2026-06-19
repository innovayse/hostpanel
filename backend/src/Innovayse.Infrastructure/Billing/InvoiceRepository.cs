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
            .Include(x => x.Transactions)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Invoice> Items, int TotalCount)> ListAsync(
        int page, int pageSize, string? status, CancellationToken ct)
    {
        var query = db.Invoices.AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            if (Enum.TryParse<InvoiceStatus>(status, out var statusEnum))
            {
                query = query.Where(x => x.Status == statusEnum);
            }
        }

        query = query.OrderByDescending(x => x.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Invoice> Items, int TotalCount)> ListAsync(
        int page, int pageSize, InvoiceStatus? status,
        DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        IQueryable<Invoice> query = db.Invoices;

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= to.Value);
        }

        var ordered = query.OrderByDescending(x => x.CreatedAt);
        var total = await ordered.CountAsync(ct);
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Invoice>> ListByClientAsync(int clientId, CancellationToken ct) =>
        await db.Invoices
            .Include(x => x.Items)
            .Include(x => x.Transactions)
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Invoice> Items, int TotalCount)> ListByClientAsync(
        int clientId, int page, int pageSize, InvoiceStatus? status,
        DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        var query = db.Invoices
            .Include(x => x.Items)
            .Include(x => x.Transactions)
            .Where(x => x.ClientId == clientId);

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= to.Value);
        }

        var ordered = query.OrderByDescending(x => x.CreatedAt);
        var total = await ordered.CountAsync(ct);
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Invoice>> FindByIdsAsync(IReadOnlyList<int> ids, CancellationToken ct) =>
        await db.Invoices
            .Include(x => x.Items)
            .Include(x => x.Transactions)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);

    /// <inheritdoc/>
    public void Add(Invoice invoice) => db.Invoices.Add(invoice);

    /// <inheritdoc/>
    public void Remove(Invoice invoice) => db.Invoices.Remove(invoice);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Invoice>> GetPaidBetweenAsync(
        DateTimeOffset start, DateTimeOffset end, CancellationToken ct) =>
        await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Paid && i.PaidAt >= start && i.PaidAt <= end)
            .OrderBy(i => i.PaidAt)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Invoice>> ListUnpaidOverdueAsync(DateTimeOffset asOf, CancellationToken ct) =>
        await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Unpaid && i.DueDate < asOf)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken ct) =>
        await db.Invoices.ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<Invoice?> FindByExternalIdAsync(string externalId, CancellationToken ct) =>
        await db.Invoices.FirstOrDefaultAsync(i => i.ExternalId == externalId, ct);
}
