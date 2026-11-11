using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Application.Notifications.Queries.GetEmailDelivery;

/// <summary>Reads the log line a keyed mail left, so a caller that sent it without waiting can learn how it ended.</summary>
public sealed class GetEmailDeliveryHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>How long a keyed delivery claimed by another attempt is trusted to still be in flight.</summary>
    private readonly IOptions<NotificationOptions> _options;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="options">How long a keyed delivery claimed by another attempt is trusted to still be in flight.</param>
    /// <param name="clock">The source of the current moment.</param>
    public GetEmailDeliveryHandler(IHostpanelDbContext context, IOptions<NotificationOptions> options, IClock clock)
    {
        _context = context;
        _options = options;
        _clock = clock;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The key of the mail.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The delivery state and whether a send under the key would go out again, or <c>EMAIL_LOG_NOT_FOUND</c> when no mail was logged under the key (a template that is switched off sends nothing).</returns>
    public async Task<Result<EmailDeliveryDto>> HandleAsync(GetEmailDeliveryQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var log = await _context.EmailLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.IdempotencyKey == query.IdempotencyKey, ct);

        return log is null
            ? Result<EmailDeliveryDto>.FailureCode(HostpanelErrorCodes.EMAIL_LOG_NOT_FOUND)
            : Result<EmailDeliveryDto>.Success(
                new EmailDeliveryDto(log.State, log.Failure, log.MayResend(_clock.UtcNow, _options.Value.DeliveryClaimLease)));
    }
}
