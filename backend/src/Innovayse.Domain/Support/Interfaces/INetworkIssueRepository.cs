namespace Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Repository abstraction for <see cref="NetworkIssue"/> persistence.
/// Implementations live in the Infrastructure layer.
/// </summary>
public interface INetworkIssueRepository
{
    /// <summary>
    /// Finds a network issue by its primary key.
    /// </summary>
    /// <param name="id">The network issue identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="NetworkIssue"/>, or <see langword="null"/> if not found.</returns>
    Task<NetworkIssue?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Stages a new network issue for insertion on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="issue">The network issue to add.</param>
    void Add(NetworkIssue issue);

    /// <summary>
    /// Stages a network issue for deletion on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="issue">The network issue to remove.</param>
    void Remove(NetworkIssue issue);

    /// <summary>
    /// Returns a paged, optionally filtered list of network issues with total count.
    /// </summary>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple of matching network issues and total count.</returns>
    Task<(IReadOnlyList<NetworkIssue> Items, int TotalCount)> ListAsync(
        int page, int pageSize, NetworkIssueStatus? status, CancellationToken ct);
}
