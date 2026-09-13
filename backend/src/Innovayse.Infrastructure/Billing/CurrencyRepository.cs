namespace Innovayse.Infrastructure.Billing;

using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="ICurrencyRepository"/>.</summary>
/// <param name="db">The application database.</param>
public sealed class CurrencyRepository(AppDbContext db) : ICurrencyRepository
{
    /// <inheritdoc/>
    public Task<Currency?> FindAsync(string code, CancellationToken ct)
    {
        var upper = code.ToUpperInvariant();
        return db.Currencies.FirstOrDefaultAsync(c => c.Code == upper, ct);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No row is marked as the base — a seed or migration failure, never a runtime state.</exception>
    public async Task<Currency> GetBaseAsync(CancellationToken ct)
        => await db.Currencies.FirstOrDefaultAsync(c => c.IsBase, ct)
           ?? throw new InvalidOperationException("No base currency is configured.");

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Currency>> ListAsync(bool enabledOnly, CancellationToken ct)
    {
        var q = db.Currencies.AsQueryable();
        if (enabledOnly) q = q.Where(c => c.IsEnabled);
        return await q.OrderByDescending(c => c.IsBase).ThenBy(c => c.Code).ToListAsync(ct);
    }

    /// <inheritdoc/>
    public void Add(Currency currency) => db.Currencies.Add(currency);
}
