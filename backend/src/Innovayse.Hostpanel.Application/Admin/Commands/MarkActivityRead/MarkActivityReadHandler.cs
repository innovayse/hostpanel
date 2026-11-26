using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;

namespace Innovayse.Hostpanel.Application.Admin.Commands.MarkActivityRead;

/// <summary>Handles <see cref="MarkActivityReadCommand"/>: remembers that the calling admin has read one event.</summary>
public sealed class MarkActivityReadHandler
{
    /// <summary>The store the read marks are kept in.</summary>
    private readonly IActivityReadStore _reads;

    /// <summary>The caller, whose subject the mark is stored under.</summary>
    private readonly ICurrentUserService _currentUser;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="reads">The store the read marks are kept in.</param>
    /// <param name="currentUser">The caller, whose subject the mark is stored under.</param>
    /// <param name="clock">The source of the current moment.</param>
    public MarkActivityReadHandler(IActivityReadStore reads, ICurrentUserService currentUser, IClock clock)
    {
        _reads = reads;
        _currentUser = currentUser;
        _clock = clock;
    }

    /// <summary>Marks the event read for the caller.</summary>
    /// <param name="command">The key of the event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, whether or not the event was already read; <c>UNAUTHORIZED</c> when the request carries no signed-in caller.</returns>
    public async Task<Result> HandleAsync(MarkActivityReadCommand command, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.FailureCode(SharedErrorCodes.Unauthorized);
        }

        await _reads.MarkReadAsync(userId, [command.EventKey], _clock.UtcNow, ct);

        return Result.Success();
    }
}
