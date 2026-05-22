namespace Innovayse.Infrastructure.Billing;

using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IClientTransactionRepository"/>.</summary>
/// <param name="db">The application database context.</param>
public sealed class ClientTransactionRepository(AppDbContext db) : IClientTransactionRepository
{
    /// <inheritdoc/>
    public async Task<ClientTransaction?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.ClientTransactions.FirstOrDefaultAsync(x => x.Id == id, ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<ClientTransaction> Items, int TotalCount)> ListByClientAsync(
        int clientId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.ClientTransactions
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
        var summary = await db.ClientTransactions
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
    public void Add(ClientTransaction transaction) => db.ClientTransactions.Add(transaction);

    /// <inheritdoc/>
    public void Remove(ClientTransaction transaction) => db.ClientTransactions.Remove(transaction);
}
