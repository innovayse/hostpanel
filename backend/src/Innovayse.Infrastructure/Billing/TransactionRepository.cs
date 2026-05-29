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
    public async Task<(IReadOnlyList<Transaction> Items, int TotalCount)> ListAsync(
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.Transactions.OrderByDescending(x => x.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Transaction>> ListByClientAsync(int clientId, CancellationToken ct) =>
        await db.Transactions
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public void Add(Transaction transaction) => db.Transactions.Add(transaction);
}
