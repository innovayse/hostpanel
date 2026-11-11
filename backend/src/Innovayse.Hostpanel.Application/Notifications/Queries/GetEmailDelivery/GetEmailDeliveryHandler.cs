using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Notifications.Queries.GetEmailDelivery;

/// <summary>Reads the log line a keyed mail left, so a caller that sent it without waiting can learn how it ended.</summary>
public sealed class GetEmailDeliveryHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetEmailDeliveryHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The key of the mail.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The delivery state, or <c>EMAIL_LOG_NOT_FOUND</c> when no mail was logged under the key (a template that is switched off sends nothing).</returns>
    public async Task<Result<EmailDeliveryDto>> HandleAsync(GetEmailDeliveryQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var delivery = await _context.EmailLogs
            .AsNoTracking()
            .Where(l => l.IdempotencyKey == query.IdempotencyKey)
            .Select(l => new EmailDeliveryDto(l.State, l.Failure))
            .FirstOrDefaultAsync(ct);

        return delivery is null
            ? Result<EmailDeliveryDto>.FailureCode(HostpanelErrorCodes.EMAIL_LOG_NOT_FOUND)
            : Result<EmailDeliveryDto>.Success(delivery);
    }
}
