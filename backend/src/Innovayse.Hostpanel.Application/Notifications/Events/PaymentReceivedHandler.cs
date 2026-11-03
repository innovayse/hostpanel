using Innovayse.Hostpanel.Application.Clients.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Notifications.Events;

/// <summary>Handles <see cref="PaymentReceivedEvent"/> and dispatches a payment confirmation email.</summary>
public sealed class PaymentReceivedHandler
{
    /// <summary>The Wolverine bus this handler dispatches the notification email through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">The Wolverine bus this handler dispatches the notification email through.</param>
    public PaymentReceivedHandler(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Resolves the client email and sends a payment-received confirmation email.</summary>
    /// <param name="evt">The payment received domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(PaymentReceivedEvent evt, CancellationToken ct)
    {
        var client = await _bus.FindClientBriefAsync(evt.ClientId, withEmail: true, ct);
        if (client is not { Email: { } email })
        {
            return;
        }

        var data = new PaymentReceivedTemplateModel(
            new PaidInvoiceTemplateData(evt.InvoiceId, evt.Amount, evt.TransactionId));

        await _bus.InvokeAsync(
            new SendEmailCommand(
                email,
                client.Language,
                "payment-received",
                data,
                new ContactCopy(evt.ClientId, ContactNotification.Invoice),
                IdempotencyKey: EmailIdempotencyKey.ForMessage(evt, "payment-received")),
            ct);
    }
}
