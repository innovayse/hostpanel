using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Queries.GetRecentActivity;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Admin.Commands.MarkAllActivityRead;

/// <summary>
/// Handles <see cref="MarkAllActivityReadCommand"/>: asks the feed for its newest events, so the set of events that
/// count is decided once, by the feed query, and marks every one the calling admin has not read yet in a single
/// batch through <see cref="IActivityReadStore"/>, the same store a single mark uses, which settles a race with
/// another call by skipping the marks already there.
/// </summary>
public sealed class MarkAllActivityReadHandler
{
    /// <summary>The message bus, for the feed query.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The store the read marks are kept in.</summary>
    private readonly IActivityReadStore _reads;

    /// <summary>The caller, whose subject the marks are stored under.</summary>
    private readonly ICurrentUserService _currentUser;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">The message bus, for the feed query.</param>
    /// <param name="reads">The store the read marks are kept in.</param>
    /// <param name="currentUser">The caller, whose subject the marks are stored under.</param>
    /// <param name="clock">The source of the current moment.</param>
    public MarkAllActivityReadHandler(IMessageBus bus, IActivityReadStore reads, ICurrentUserService currentUser, IClock clock)
    {
        _bus = bus;
        _reads = reads;
        _currentUser = currentUser;
        _clock = clock;
    }

    /// <summary>Marks every unread event of the feed read for the caller.</summary>
    /// <param name="command">The command; it carries nothing.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, also when nothing was unread; the failure of the feed; <c>UNAUTHORIZED</c> without a signed-in caller.</returns>
    public async Task<Result> HandleAsync(MarkAllActivityReadCommand command, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.FailureCode(SharedErrorCodes.Unauthorized);
        }

        var feed = await _bus.InvokeAsync<Result<IReadOnlyList<ActivityEventDto>>>(
            new GetRecentActivityQuery(GetRecentActivityQuery.MaxLimit), ct);
        if (feed.IsFailure)
        {
            return Result.FailureCode(feed.ErrorCode!);
        }

        var unread = feed.Value!.Where(e => !e.IsRead).Select(e => e.Key).ToList();
        await _reads.MarkReadAsync(userId, unread, _clock.UtcNow, ct);

        return Result.Success();
    }
}
