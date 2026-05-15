namespace Innovayse.Application.Notifications.Events;

using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Domain.Billing.Events;
using Wolverine;

/// <summary>
/// Handles <see cref="PaymentReceivedEvent"/> and dispatches a payment confirmation email.
/// </summary>
/// <remarks>
/// The handler does not have the client email directly from the event.
/// TODO: Resolve client email via IClientRepository if email delivery is required.
/// </remarks>
public sealed class PaymentReceivedHandler(IMessageBus bus)
{
    /// <summary>
    /// Placeholder — currently a no-op until client email lookup is wired up.
    /// </summary>
    /// <param name="evt">The payment received domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A completed task.</returns>
    public Task HandleAsync(PaymentReceivedEvent evt, CancellationToken ct)
    {
        // TODO: Resolve client email from IClientRepository, then dispatch:
        // var data = new { invoice = new { id = evt.InvoiceId, total = evt.Amount } };
        // await bus.InvokeAsync(new SendEmailCommand(clientEmail, "payment-received", data), ct);
        _ = bus;
        _ = evt;
        _ = ct;
        return Task.CompletedTask;
    }
}
