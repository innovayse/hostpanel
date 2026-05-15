namespace Innovayse.Domain.Services.Interfaces;

/// <summary>
/// Persistence contract for <see cref="CancellationRequest"/> entity operations.
/// </summary>
public interface ICancellationRequestRepository
{
    /// <summary>Returns a paginated list of cancellation requests ordered by creation date descending.</summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Items per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated items and total count.</returns>
    Task<(IReadOnlyList<CancellationRequest> Items, int TotalCount)> ListAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>Finds a cancellation request by primary key, or returns <see langword="null"/>.</summary>
    /// <param name="id">Primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="CancellationRequest"/>, or <see langword="null"/> if not found.</returns>
    Task<CancellationRequest?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>Finds an existing cancellation request for the given service, or returns <see langword="null"/>.</summary>
    /// <param name="serviceId">FK to the client service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="CancellationRequest"/>, or <see langword="null"/>.</returns>
    Task<CancellationRequest?> FindByServiceIdAsync(int serviceId, CancellationToken ct);

    /// <summary>Adds a new cancellation request. Call SaveChangesAsync to persist.</summary>
    /// <param name="request">The new cancellation request entity.</param>
    void Add(CancellationRequest request);

    /// <summary>Removes a cancellation request. Call SaveChangesAsync to persist.</summary>
    /// <param name="request">The cancellation request entity to remove.</param>
    void Remove(CancellationRequest request);
}
