using System.Globalization;
using Innovayse.Hostpanel.Application.Clients.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Domain.Domains.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Domains.Events;

/// <summary>
/// Handles <see cref="DomainExpiringEvent"/> and dispatches a domain-expiring notification email
/// to the owning client.
/// </summary>
public sealed class DomainExpiringNotificationHandler
{
    /// <summary>Message bus, for dispatching the notification email command.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<DomainExpiringNotificationHandler> _logger;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>The data context the domain is read from, to confirm it still belongs to the event's client.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">Message bus, for dispatching the notification email command.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The source of the current moment.</param>
    /// <param name="context">The data context the domain is read from, to confirm it still belongs to the event's client.</param>
    public DomainExpiringNotificationHandler(
        IMessageBus bus,
        ILogger<DomainExpiringNotificationHandler> logger,
        IClock clock,
        IHostpanelDbContext context)
    {
        _bus = bus;
        _logger = logger;
        _clock = clock;
        _context = context;
    }

    /// <summary>
    /// Resolves the client email and sends a domain-expiring notification email.
    /// </summary>
    /// <param name="evt">The domain expiring domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(DomainExpiringEvent evt, CancellationToken ct)
    {
        try
        {
            // The event arrives through a durable queue, so it can arrive after the demo reset truncated
            // the tables and reused the ids: a domain that is gone or now belongs to someone else is not
            // the one this mail is about, and the client the event names may be a different account now.
            var owned = await _context.Domains.AsNoTracking()
                .AnyAsync(x => x.Id == evt.DomainId && x.ClientId == evt.ClientId, ct);
            if (!owned)
            {
                _logger.LogInformation(
                    "Domain {DomainId} of client {ClientId} no longer exists; no domain-expiring notification is sent.",
                    evt.DomainId,
                    evt.ClientId);
                return;
            }

            var client = await _bus.FindClientBriefAsync(evt.ClientId, withEmail: true, ct);
            if (client is null)
            {
                _logger.LogWarning(
                    "Client {ClientId} not found for domain expiring notification (domain {DomainId}).",
                    evt.ClientId,
                    evt.DomainId);
                return;
            }

            var email = client.Email;
            if (email is null)
            {
                _logger.LogWarning(
                    "User {UserId} not found for domain expiring notification (domain {DomainId}).",
                    client.UserSubject,
                    evt.DomainId);
                return;
            }

            var daysUntilExpiry = (int)Math.Ceiling((evt.ExpiresAt - _clock.UtcNow).TotalDays);

            var data = new DomainExpiringTemplateModel(new ExpiringDomainTemplateData(
                evt.DomainId,
                evt.Name,
                evt.ExpiresAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                daysUntilExpiry));

            await _bus.InvokeAsync(
                new SendEmailCommand(
                    email,
                    client.Language,
                    "domain-expiring",
                    data,
                    new ContactCopy(evt.ClientId, ContactNotification.Domain),
                    IdempotencyKey: EmailIdempotencyKey.ForMessage(evt, "domain-expiring")),
                ct);

            _logger.LogInformation(
                "Sent domain-expiring notification for domain {DomainName} (ID {DomainId}, expires in {Days} days) to {Email}.",
                evt.Name,
                evt.DomainId,
                daysUntilExpiry,
                email);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send domain-expiring notification for domain {DomainId}.",
                evt.DomainId);
        }
    }
}
