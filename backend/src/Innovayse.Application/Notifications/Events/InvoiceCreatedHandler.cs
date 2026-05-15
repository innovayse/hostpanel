namespace Innovayse.Application.Notifications.Events;

using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Domain.Billing.Events;
using Wolverine;

/// <summary>
/// Handles <see cref="InvoiceCreatedEvent"/> and dispatches an invoice-created notification email.
/// </summary>
/// <remarks>
/// The handler does not have the client email directly from the event.
/// TODO: Resolve client email via IClientRepository if email delivery is required.
/// </remarks>
public sealed class InvoiceCreatedHandler(IMessageBus bus)
{
    /// <summary>
    /// Placeholder — currently a no-op until client email lookup is wired up.
    /// </summary>
    /// <param name="evt">The invoice created domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A completed task.</returns>
    public Task HandleAsync(InvoiceCreatedEvent evt, CancellationToken ct)
    {
        // TODO: Resolve client email from IClientRepository, then dispatch:
        // var data = new { invoice = new { id = evt.InvoiceId } };
        // await bus.InvokeAsync(new SendEmailCommand(clientEmail, "invoice-created", data), ct);
        _ = bus;
        _ = evt;
        _ = ct;
        return Task.CompletedTask;
    }
}
