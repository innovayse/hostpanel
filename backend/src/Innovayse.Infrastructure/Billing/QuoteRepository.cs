namespace Innovayse.Infrastructure.Billing;

using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IQuoteRepository"/>.</summary>
/// <param name="db">The application database context.</param>
public sealed class QuoteRepository(AppDbContext db) : IQuoteRepository
{
    /// <inheritdoc/>
    public async Task<Quote?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.Quotes
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Quote> Items, int TotalCount)> ListAsync(
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.Quotes.OrderByDescending(x => x.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Quote>> ListByClientAsync(int clientId, CancellationToken ct) =>
        await db.Quotes
            .Include(x => x.Items)
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public void Add(Quote quote) => db.Quotes.Add(quote);

    /// <inheritdoc/>
    public void Delete(Quote quote) => db.Quotes.Remove(quote);
}
