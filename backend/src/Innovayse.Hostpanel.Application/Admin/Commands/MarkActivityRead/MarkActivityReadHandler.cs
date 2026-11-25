using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Admin;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Commands.MarkActivityRead;

/// <summary>Handles <see cref="MarkActivityReadCommand"/>: remembers that the calling admin has read one event.</summary>
public sealed class MarkActivityReadHandler
{
    /// <summary>The data context, where the read marks are kept.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The caller, whose subject the mark is stored under.</summary>
    private readonly ICurrentUserService _currentUser;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context, where the read marks are kept.</param>
    /// <param name="currentUser">The caller, whose subject the mark is stored under.</param>
    /// <param name="clock">The source of the current moment.</param>
    public MarkActivityReadHandler(IHostpanelDbContext context, ICurrentUserService currentUser, IClock clock)
    {
        _context = context;
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

        var alreadyRead = await _context.ActivityReads.AnyAsync(r => r.UserId == userId && r.EventKey == command.EventKey, ct);
        if (alreadyRead)
        {
            return Result.Success();
        }

        try
        {
            _context.ActivityReads.Add(ActivityRead.Create(userId, command.EventKey, _clock.UtcNow));
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // Another call marked the same event between the check and the save: the event is read either way.
        }

        return Result.Success();
    }
}
