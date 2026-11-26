namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Records which events of the notification feed an admin has read. The one place a read mark is written, so a
/// single mark and a mark-all settle a race with another call the same way.
/// </summary>
public interface IActivityReadStore
{
    /// <summary>
    /// Marks the events read for an admin. An event already marked, also by a call running at the same moment,
    /// is left as it is: nothing fails and no duplicate is stored.
    /// </summary>
    /// <param name="userId">The subject of the admin who read the events.</param>
    /// <param name="eventKeys">The keys of the events (see <c>ActivityEventDto.Key</c>).</param>
    /// <param name="readAt">The moment they were read.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>A task that completes once every missing mark is stored.</returns>
    public Task MarkReadAsync(string userId, IReadOnlyCollection<string> eventKeys, DateTimeOffset readAt, CancellationToken ct);
}
