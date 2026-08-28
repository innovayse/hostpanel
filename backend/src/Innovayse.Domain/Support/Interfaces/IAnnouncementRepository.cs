namespace Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Repository abstraction for <see cref="Announcement"/> entity persistence.
/// Implementations live in the Infrastructure layer.
/// </summary>
public interface IAnnouncementRepository
{
    /// <summary>
    /// Finds an announcement by its primary key.
    /// </summary>
    /// <param name="id">The announcement identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="Announcement"/>, or <see langword="null"/> if not found.</returns>
    Task<Announcement?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Stages a new announcement for insertion on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="announcement">The announcement to add.</param>
    void Add(Announcement announcement);

    /// <summary>
    /// Stages an announcement for deletion on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="announcement">The announcement to remove.</param>
    void Remove(Announcement announcement);

    /// <summary>
    /// Returns a paginated list of announcements ordered by creation date descending.
    /// </summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple of the paged items and the total count across all pages.</returns>
    Task<(IReadOnlyList<Announcement> Items, int TotalCount)> ListAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>
    /// Returns a paginated list of <em>published</em> announcements ordered by creation date
    /// descending — the rows a client is allowed to see.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="ListAsync"/> rather than a boolean on it: the filter has to be
    /// inside the query that pages, so <c>TotalCount</c> and the page numbers count only visible
    /// rows, and a client-facing caller cannot reach the unfiltered set by passing the wrong
    /// argument.
    /// </remarks>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple of the paged published items and the total count of published rows.</returns>
    Task<(IReadOnlyList<Announcement> Items, int TotalCount)> ListPublishedAsync(int page, int pageSize, CancellationToken ct);
}
