using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Notifications.Queries.GetClientEmailLog;

/// <summary>Returns one email a client was sent.</summary>
public sealed class GetClientEmailLogHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, used to resolve the client's sign-in address through the Clients module; a log entry is keyed by address, not by id.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine bus, used to resolve the client's sign-in address through the Clients module.</param>
    public GetClientEmailLogHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The client and the email log entry to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The entry, or <c>EMAIL_LOG_NOT_FOUND</c> when it does not exist, was sent to another
    /// client, or the client id itself names nobody; all answered alike deliberately: telling
    /// them apart would confirm that an entry exists to whoever guessed its id.
    /// </returns>
    public async Task<Result<EmailLogDetailDto>> HandleAsync(GetClientEmailLogQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var client = await _bus.FindClientBriefAsync(query.ClientId, withEmail: true, ct);
        if (client is not { Email: { } email })
        {
            return Result<EmailLogDetailDto>.FailureCode(HostpanelErrorCodes.EMAIL_LOG_NOT_FOUND);
        }

        var log = await _context.EmailLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == query.EmailLogId && l.To == email, ct);
        if (log is null)
        {
            return Result<EmailLogDetailDto>.FailureCode(HostpanelErrorCodes.EMAIL_LOG_NOT_FOUND);
        }

        return Result<EmailLogDetailDto>.Success(
            new EmailLogDetailDto(log.Id, log.To, log.Subject, log.Body, log.SentAt, log.Success, log.Failure));
    }
}
