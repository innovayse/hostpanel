using Innovayse.Hostpanel.Application.Clients.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Services.Commands.SuspendService;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Domain.Domains.Events;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Domains.Events;

/// <summary>
/// Handles <see cref="DomainExpiredEvent"/> raised when a domain transitions to the Expired status.
/// Dispatches a <see cref="SuspendServiceCommand"/> for the linked hosting service, if any,
/// and sends a domain-expired notification email to the owning client.
/// </summary>
public sealed class DomainExpiredHandler
{
    /// <summary>Wolverine bus used to suspend the linked service and to send the notification email.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The data context the domain is read from.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<DomainExpiredHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">Wolverine bus used to suspend the linked service and to send the notification email.</param>
    /// <param name="context">The data context the domain is read from.</param>
    /// <param name="logger">Structured logger.</param>
    public DomainExpiredHandler(
        IMessageBus bus, IHostpanelDbContext context, ILogger<DomainExpiredHandler> logger)
    {
        _bus = bus;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Handles the domain expired event by suspending the linked hosting service
    /// and sending a notification email.
    /// </summary>
    /// <param name="evt">The domain expired event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task HandleAsync(DomainExpiredEvent evt, CancellationToken ct)
    {
        _logger.LogInformation(
            "Domain {DomainId} (client {ClientId}) has expired.",
            evt.DomainId,
            evt.ClientId);

        var domain = await _context.Domains
            .IncludeChildren()
            .FirstOrDefaultAsync(x => x.Id == evt.DomainId, ct);

        // The event arrives through a durable queue, so it can be redelivered after a crash, retried
        // minutes later, or arrive after the demo reset truncated the tables and reused the ids. A
        // domain that is gone or now belongs to someone else is not the one that expired, and a domain
        // renewed since is no longer expired: suspending on a stale copy would take down a service the
        // event never named.
        if (domain is null || domain.ClientId != evt.ClientId)
        {
            _logger.LogInformation(
                "Domain {DomainId} of client {ClientId} no longer exists; the expiry is not acted on.",
                evt.DomainId,
                evt.ClientId);
            return;
        }

        if (evt.LinkedServiceId.HasValue
            && domain.LinkedServiceId == evt.LinkedServiceId
            && domain.Status is DomainStatus.Expired or DomainStatus.Redemption)
        {
            await SuspendLinkedServiceAsync(evt.DomainId, evt.LinkedServiceId.Value, ct);
        }

        await SendNotificationAsync(evt, domain, ct);
    }

    /// <summary>
    /// Suspends the expired domain's linked hosting service. A replay is harmless: the services module
    /// refuses to suspend a service that is already suspended, and the refusal is only logged.
    /// </summary>
    /// <param name="domainId">The expired domain ID.</param>
    /// <param name="linkedServiceId">The linked hosting service ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task SuspendLinkedServiceAsync(int domainId, int linkedServiceId, CancellationToken ct)
    {
        try
        {
            var outcome = await _bus.InvokeAsync<Result>(
                new SuspendServiceCommand(linkedServiceId, SuspensionSource.DomainExpired), ct);

            if (outcome.IsSuccess)
            {
                _logger.LogInformation(
                    "Suspended linked service {ServiceId} due to domain {DomainId} expiry.",
                    linkedServiceId,
                    domainId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to suspend linked service {ServiceId} for expired domain {DomainId} ({Code}). The service may need manual suspension.",
                    linkedServiceId,
                    domainId,
                    outcome.ErrorCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to suspend linked service {ServiceId} for expired domain {DomainId}. The service may need manual suspension.",
                linkedServiceId,
                domainId);
        }
    }

    /// <summary>
    /// Sends a domain-expired notification email to the owning client.
    /// Failures are logged as warnings and do not propagate.
    /// </summary>
    /// <param name="evt">The domain expired event.</param>
    /// <param name="domain">The expired domain, already checked to belong to the event's client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    private async Task SendNotificationAsync(DomainExpiredEvent evt, RegisteredDomain domain, CancellationToken ct)
    {
        try
        {
            var client = await _bus.FindClientBriefAsync(evt.ClientId, withEmail: true, ct);
            if (client is null)
            {
                return;
            }

            var email = client.Email;
            if (email is null)
            {
                return;
            }

            var data = new DomainExpiredTemplateModel(
                new ExpiredDomainTemplateData(evt.DomainId, domain.Name, evt.LinkedServiceId));

            await _bus.InvokeAsync(new SendEmailCommand(email, client.Language, "domain-expired", data, new ContactCopy(evt.ClientId, ContactNotification.Domain)), ct);

            _logger.LogInformation(
                "Sent domain-expired notification for domain {DomainId} to {Email}.",
                evt.DomainId,
                email);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send domain-expired notification for domain {DomainId}.",
                evt.DomainId);
        }
    }
}
