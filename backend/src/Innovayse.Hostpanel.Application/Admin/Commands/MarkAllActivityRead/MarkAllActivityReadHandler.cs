using Innovayse.Hostpanel.Application.Admin.Commands.MarkActivityRead;
using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Queries.GetRecentActivity;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Admin.Commands.MarkAllActivityRead;

/// <summary>
/// Handles <see cref="MarkAllActivityReadCommand"/>: asks the feed for its newest events and marks every one the
/// calling admin has not read yet through <see cref="MarkActivityReadCommand"/>, so the set of events that count is
/// decided once, by the feed query, and a mark that races another call is settled once, by the single mark. Each
/// mark is saved on its own, so a conflict on one event never drops the others.
/// </summary>
public sealed class MarkAllActivityReadHandler
{
    /// <summary>The message bus, for the feed query and the single marks.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">The message bus, for the feed query and the single marks.</param>
    public MarkAllActivityReadHandler(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Marks every unread event of the feed read for the caller.</summary>
    /// <param name="command">The command; it carries nothing.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, also when nothing was unread; the failure of the feed or of a mark (<c>UNAUTHORIZED</c> without a signed-in caller).</returns>
    public async Task<Result> HandleAsync(MarkAllActivityReadCommand command, CancellationToken ct)
    {
        var feed = await _bus.InvokeAsync<Result<IReadOnlyList<ActivityEventDto>>>(
            new GetRecentActivityQuery(GetRecentActivityQuery.MaxLimit), ct);
        if (feed.IsFailure)
        {
            return Result.FailureCode(feed.ErrorCode!);
        }

        foreach (var unread in feed.Value!.Where(e => !e.IsRead))
        {
            var marked = await _bus.InvokeAsync<Result>(new MarkActivityReadCommand(unread.Key), ct);
            if (marked.IsFailure)
            {
                return marked;
            }
        }

        return Result.Success();
    }
}
