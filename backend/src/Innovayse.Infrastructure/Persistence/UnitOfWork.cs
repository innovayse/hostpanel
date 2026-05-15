namespace Innovayse.Infrastructure.Persistence;

using Innovayse.Application.Common;

/// <summary>
/// EF Core implementation of <see cref="IUnitOfWork"/>.
/// Delegates to <see cref="Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync(CancellationToken)"/>.
/// </summary>
/// <param name="db">The application DbContext.</param>
public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    /// <inheritdoc/>
    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
