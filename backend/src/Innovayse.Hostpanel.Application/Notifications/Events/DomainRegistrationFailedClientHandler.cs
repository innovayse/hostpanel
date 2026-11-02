using Innovayse.Hostpanel.Application.Clients.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Domain.Domains.Events;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Notifications.Events;

/// <summary>
/// Handles <see cref="DomainRegistrationFailedEvent"/> and notifies the affected client
/// by email that their domain registration failed and a refund has been issued.
/// </summary>
public sealed class DomainRegistrationFailedClientHandler
{
    /// <summary>The Wolverine bus this handler dispatches the notification email through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">The Wolverine bus this handler dispatches the notification email through.</param>
    public DomainRegistrationFailedClientHandler(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Resolves the client email and dispatches a domain-registration-failed notification email.</summary>
    /// <param name="evt">The domain registration failed event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(DomainRegistrationFailedEvent evt, CancellationToken ct)
    {
        var client = await _bus.FindClientBriefAsync(evt.ClientId, withEmail: true, ct);
        if (client is not { Email: { } email })
        {
            return;
        }

        var data = new DomainRegistrationFailedTemplateModel(
            new DomainNameTemplateData(evt.DomainName),
            new InvoiceRefTemplateData(evt.InvoiceId));

        await _bus.InvokeAsync(
            new SendEmailCommand(
                email,
                client.Language,
                "domain-registration-failed",
                data,
                new ContactCopy(evt.ClientId, ContactNotification.Domain),
                IdempotencyKey: EmailIdempotencyKey.ForMessage(evt, "domain-registration-failed")),
            ct);
    }
}
