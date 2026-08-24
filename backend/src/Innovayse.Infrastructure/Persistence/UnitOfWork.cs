namespace Innovayse.Infrastructure.Persistence;

using Innovayse.Application.Common;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of <see cref="IUnitOfWork"/>.
/// Delegates to <see cref="Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync(CancellationToken)"/>.
/// </summary>
/// <param name="db">The application DbContext.</param>
public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    /// <inheritdoc/>
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Translate the EF-specific exception to a persistence-agnostic one — the
            // Application layer must not reference EF Core (see
            // rules/architectures/clean-architecture.md), so this is the only place allowed
            // to know that the underlying cause was an optimistic-concurrency conflict.
            throw new ConcurrencyConflictException(
                "Another writer modified one of the saved aggregates concurrently.", ex);
        }
    }

    /// <inheritdoc/>
    public void DetachAll()
    {
        foreach (var entry in db.ChangeTracker.Entries().ToList())
        {
            entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        }
    }
}
