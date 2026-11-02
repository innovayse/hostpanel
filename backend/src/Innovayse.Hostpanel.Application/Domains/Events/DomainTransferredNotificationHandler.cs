using System.Globalization;
using Innovayse.Hostpanel.Application.Clients.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Domain.Domains.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Domains.Events;

/// <summary>
/// Handles <see cref="DomainTransferredInEvent"/> and dispatches a domain-transferred notification email
/// to the owning client.
/// </summary>
public sealed class DomainTransferredNotificationHandler
{
    /// <summary>Wolverine bus used to send the notification email.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The data context the domain is read from.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<DomainTransferredNotificationHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">Wolverine bus used to send the notification email.</param>
    /// <param name="context">The data context the domain is read from.</param>
    /// <param name="logger">Structured logger.</param>
    public DomainTransferredNotificationHandler(
        IMessageBus bus,
        IHostpanelDbContext context,
        ILogger<DomainTransferredNotificationHandler> logger)
    {
        _bus = bus;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the client email and sends a domain-transferred notification email.
    /// </summary>
    /// <param name="evt">The domain transferred-in domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(DomainTransferredInEvent evt, CancellationToken ct)
    {
        try
        {
            var domain = await _context.Domains
                .IncludeChildren()
                .FirstOrDefaultAsync(x => x.Id == evt.DomainId, ct);

            // The event arrives through a durable queue, so it can arrive after the demo reset truncated
            // the tables and reused the ids: a domain that is gone or now belongs to someone else is not
            // the one this mail is about, and the client the event names may be a different account now.
            if (domain is null || domain.ClientId != evt.ClientId)
            {
                _logger.LogInformation(
                    "Domain {DomainId} of client {ClientId} no longer exists; no domain-transferred notification is sent.",
                    evt.DomainId,
                    evt.ClientId);
                return;
            }

            var client = await _bus.FindClientBriefAsync(evt.ClientId, withEmail: true, ct);
            if (client is null)
            {
                _logger.LogWarning(
                    "Client {ClientId} not found for domain transferred notification (domain {DomainId}).",
                    evt.ClientId,
                    evt.DomainId);
                return;
            }

            var email = client.Email;
            if (email is null)
            {
                _logger.LogWarning(
                    "User {UserId} not found for domain transferred notification (domain {DomainId}).",
                    client.UserSubject,
                    evt.DomainId);
                return;
            }

            var data = new DomainTransferredTemplateModel(new RegisteredDomainTemplateData(
                evt.DomainId,
                evt.Name,
                domain.Registrar,
                domain.ExpiresAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

            await _bus.InvokeAsync(
                new SendEmailCommand(
                    email,
                    client.Language,
                    "domain-transferred",
                    data,
                    new ContactCopy(evt.ClientId, ContactNotification.Domain),
                    IdempotencyKey: EmailIdempotencyKey.ForMessage(evt, "domain-transferred")),
                ct);

            _logger.LogInformation(
                "Sent domain-transferred notification for domain {DomainName} (ID {DomainId}) to {Email}.",
                evt.Name,
                evt.DomainId,
                email);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send domain-transferred notification for domain {DomainId}.",
                evt.DomainId);
        }
    }
}
