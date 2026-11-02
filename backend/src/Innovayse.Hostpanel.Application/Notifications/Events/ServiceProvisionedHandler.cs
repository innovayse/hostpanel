using Innovayse.Hostpanel.Application.Clients.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Services.Queries.GetService;
using Innovayse.Hostpanel.Domain.Provisioning.Events;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Notifications.Events;

/// <summary>Handles <see cref="ServiceProvisionedEvent"/> and dispatches a service-ready notification email.</summary>
public sealed class ServiceProvisionedHandler
{
    /// <summary>The Wolverine bus this handler dispatches the notification email through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">The Wolverine bus this handler dispatches the notification email through.</param>
    public ServiceProvisionedHandler(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Resolves the client email and sends a service-provisioned notification email.</summary>
    /// <param name="evt">The service provisioned domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(ServiceProvisionedEvent evt, CancellationToken ct)
    {
        var service = (await _bus.InvokeAsync<Result<ServiceDetailDto>>(new GetServiceQuery(evt.ServiceId), ct)).Value;
        if (service is null)
        {
            return;
        }

        var client = await _bus.FindClientBriefAsync(evt.ClientId, withEmail: true, ct);
        if (client is not { Email: { } email })
        {
            return;
        }

        var data = new ServiceProvisionedTemplateModel(
            new ProvisionedServiceTemplateData(evt.ServiceId, evt.ProvisioningRef, service.Domain, service.Account.Username));

        await _bus.InvokeAsync(
            new SendEmailCommand(
                email,
                client.Language,
                "service-provisioned",
                data,
                new ContactCopy(evt.ClientId, ContactNotification.Product),
                IdempotencyKey: EmailIdempotencyKey.ForMessage(evt, "service-provisioned")),
            ct);
    }
}
