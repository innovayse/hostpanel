namespace Innovayse.Infrastructure.Billing;

using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="ITransactionRepository"/>.</summary>
/// <param name="db">The application database context.</param>
public sealed class TransactionRepository(AppDbContext db) : ITransactionRepository
{
    /// <inheritdoc/>
    public async Task<Transaction?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.Transactions.FirstOrDefaultAsync(x => x.Id == id, ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Transaction> Items, int TotalCount)> ListByClientAsync(
        int clientId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Transactions
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.Date);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<(decimal TotalIn, decimal TotalOut, decimal TotalFees)> GetClientSummaryAsync(
        int clientId, CancellationToken ct)
    {
        var summary = await db.Transactions
            .Where(x => x.ClientId == clientId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalIn = g.Sum(x => x.AmountIn),
                TotalOut = g.Sum(x => x.AmountOut),
                TotalFees = g.Sum(x => x.Fees),
            })
            .FirstOrDefaultAsync(ct);

        return summary is null
            ? (0m, 0m, 0m)
            : (summary.TotalIn, summary.TotalOut, summary.TotalFees);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Transaction> Items, int TotalCount)> ListAllAsync(
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.Transactions.OrderByDescending(x => x.Date);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    /// <inheritdoc/>
    public void Add(Transaction transaction) => db.Transactions.Add(transaction);

    /// <inheritdoc/>
    public void Remove(Transaction transaction) => db.Transactions.Remove(transaction);
}
