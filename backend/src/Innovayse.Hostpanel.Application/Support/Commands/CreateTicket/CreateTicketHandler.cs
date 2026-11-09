using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Support.Extensions;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Support.Commands.CreateTicket;

/// <summary>Creates a new support ticket on the account the caller acts for.</summary>
public sealed class CreateTicketHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the account the ticket is filed on, owner or linked user; the command does not say, and must not.</summary>
    private readonly IClientAccess _access;

    /// <summary>The clock that dates the new ticket.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="access">Resolves the account the ticket is filed on, owner or linked user.</param>
    /// <param name="clock">The clock that dates the new ticket.</param>
    public CreateTicketHandler(IHostpanelDbContext context, IClientAccess access, IClock clock)
    {
        _context = context;
        _access = access;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The command. It names no account: the ticket is filed on the one the caller acts for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with the new ticket's id, <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller acts for no
    /// account, or <c>DEPARTMENT_NOT_FOUND</c> when the department id names no department.
    /// </returns>
    public async Task<Result<int>> HandleAsync(CreateTicketCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var clientId = await _access.RequireClientIdAsync(ct);
        if (clientId.IsFailure)
        {
            return Result<int>.FailureCode(clientId.ErrorCode!);
        }

        if (!await _context.DepartmentExistsAsync(cmd.DepartmentId!.Value, ct))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.DEPARTMENT_NOT_FOUND);
        }

        var ticket = Ticket.Create(clientId.Value, cmd.Subject, cmd.Message, cmd.DepartmentId!.Value, cmd.Priority, _clock.UtcNow);

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync(ct);

        return Result.Success(ticket.Id);
    }
}
