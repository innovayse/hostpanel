namespace Innovayse.Application.Notifications.Events;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Domain.Billing.Events;
using Innovayse.Domain.Clients.Interfaces;
using Wolverine;

/// <summary>
/// Handles <see cref="PaymentReceivedEvent"/> and dispatches a payment confirmation email.
/// </summary>
public sealed class PaymentReceivedHandler(
    IMessageBus bus,
    IClientRepository clientRepo,
    IUserService userService)
{
    /// <summary>
    /// Resolves the client email and sends a payment-received confirmation email.
    /// </summary>
    /// <param name="evt">The payment received domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(PaymentReceivedEvent evt, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(evt.ClientId, ct);
        if (client is null) return;

        var user = await userService.FindByIdAsync(client.UserId, ct);
        if (user is null) return;

        var data = new
        {
            invoice = new
            {
                id = evt.InvoiceId,
                amount = evt.Amount,
                transactionId = evt.TransactionId
            }
        };

        await bus.InvokeAsync(new SendEmailCommand(user.Value.Email, "payment-received", data), ct);
    }
}
