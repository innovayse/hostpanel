namespace Innovayse.Application.Common;

/// <summary>
/// Abstracts the persistence transaction boundary.
/// Infrastructure implements this; Application calls it at the end of each command handler.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists all pending changes to the database in a single transaction.
    /// Domain events on modified aggregates are dispatched by Wolverine after this call.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
