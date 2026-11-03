using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Domain.Clients.Events;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Notifications.Events;

/// <summary>Handles <see cref="ClientCreatedEvent"/> and dispatches a welcome email to the new client.</summary>
public sealed class ClientRegisteredHandler
{
    /// <summary>The Wolverine bus this handler dispatches the notification email through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">The Wolverine bus this handler dispatches the notification email through.</param>
    public ClientRegisteredHandler(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Sends a welcome email when a new client registers.</summary>
    /// <param name="evt">The client created domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(ClientCreatedEvent evt, CancellationToken ct)
    {
        // The brief carries the language the person chose at sign-up; a record already gone again
        // still gets its welcome, in the default language.
        var client = await _bus.FindClientBriefAsync(evt.ClientId, withEmail: true, ct);
        var data = new WelcomeTemplateModel(new WelcomeClientTemplateData(evt.Email));
        await _bus.InvokeAsync(
            new SendEmailCommand(
                evt.Email,
                client?.Language,
                "welcome",
                data,
                IdempotencyKey: EmailIdempotencyKey.ForMessage(evt, "welcome")),
            ct);
    }
}
