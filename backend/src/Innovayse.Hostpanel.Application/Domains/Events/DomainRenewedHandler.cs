using System.Globalization;
using Innovayse.Hostpanel.Application.Clients.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Services.Commands.LiftDomainExpirySuspension;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Events;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Domains.Events;

/// <summary>
/// Handles <see cref="DomainRenewedEvent"/> raised when a domain is successfully renewed.
/// If the domain has a linked hosting service, dispatches <see cref="LiftDomainExpirySuspensionCommand"/>
/// so the services module lifts a suspension the domain's expiry caused.
/// Also sends a domain-renewed notification email to the owning client.
/// </summary>
public sealed class DomainRenewedHandler
{
    /// <summary>The data context the domain is read from.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus used to lift the linked service's expiry suspension and to send the notification email.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<DomainRenewedHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context the domain is read from.</param>
    /// <param name="bus">Wolverine bus used to lift the linked service's expiry suspension and to send the notification email.</param>
    /// <param name="logger">Structured logger.</param>
    public DomainRenewedHandler(
        IHostpanelDbContext context, IMessageBus bus, ILogger<DomainRenewedHandler> logger)
    {
        _context = context;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>
    /// Handles the domain renewed event by unsuspending the linked hosting service when applicable.
    /// </summary>
    /// <param name="evt">The domain renewed event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task HandleAsync(DomainRenewedEvent evt, CancellationToken ct)
    {
        _logger.LogInformation(
            "Domain {DomainId} (client {ClientId}) has been renewed until {NewExpiresAt}.",
            evt.DomainId,
            evt.ClientId,
            evt.NewExpiresAt);

        var domain = await _context.Domains
            .IncludeChildren()
            .FirstOrDefaultAsync(x => x.Id == evt.DomainId, ct);

        // The event arrives through a durable queue, so it can arrive after the demo reset truncated
        // the tables and reused the ids: a domain that is gone or now belongs to someone else is not the
        // one that was renewed, and lifting or mailing on its behalf would touch an unrelated account.
        // A plain redelivery is harmless: the lift only clears an expiry suspension that is still there.
        if (domain is null || domain.ClientId != evt.ClientId)
        {
            _logger.LogInformation(
                "Domain {DomainId} of client {ClientId} no longer exists; the renewal is not acted on.",
                evt.DomainId,
                evt.ClientId);
            return;
        }

        if (domain.LinkedServiceId is { } linkedServiceId)
        {
            await TryUnsuspendLinkedServiceAsync(evt.DomainId, linkedServiceId, ct);
        }

        await SendNotificationAsync(evt, domain, ct);
    }

    /// <summary>
    /// Asks the services module to lift the linked hosting service's expiry suspension after the domain
    /// is renewed; it decides whether there is one to lift.
    /// </summary>
    /// <param name="domainId">The renewed domain ID.</param>
    /// <param name="linkedServiceId">The linked hosting service ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task TryUnsuspendLinkedServiceAsync(int domainId, int linkedServiceId, CancellationToken ct)
    {
        try
        {
            var outcome = await _bus.InvokeAsync<Result>(new LiftDomainExpirySuspensionCommand(linkedServiceId), ct);

            if (outcome.IsFailure)
            {
                _logger.LogWarning(
                    "Failed to unsuspend linked service {ServiceId} for renewed domain {DomainId} ({Code}). The service may need manual reactivation.",
                    linkedServiceId,
                    domainId,
                    outcome.ErrorCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to unsuspend linked service {ServiceId} for renewed domain {DomainId}. The service may need manual reactivation.",
                linkedServiceId,
                domainId);
        }
    }

    /// <summary>
    /// Sends a domain-renewed notification email to the owning client.
    /// Failures are logged as warnings and do not propagate.
    /// </summary>
    /// <param name="evt">The domain renewed event.</param>
    /// <param name="domain">The renewed domain, already checked to belong to the event's client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    private async Task SendNotificationAsync(DomainRenewedEvent evt, RegisteredDomain domain, CancellationToken ct)
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

            var data = new DomainRenewedTemplateModel(new RenewedDomainTemplateData(
                evt.DomainId,
                domain.Name,
                evt.NewExpiresAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

            await _bus.InvokeAsync(
                new SendEmailCommand(
                    email,
                    client.Language,
                    "domain-renewed",
                    data,
                    new ContactCopy(evt.ClientId, ContactNotification.Domain),
                    IdempotencyKey: EmailIdempotencyKey.ForMessage(evt, "domain-renewed")),
                ct);

            _logger.LogInformation(
                "Sent domain-renewed notification for domain {DomainId} to {Email}.",
                evt.DomainId,
                email);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send domain-renewed notification for domain {DomainId}.",
                evt.DomainId);
        }
    }
}
