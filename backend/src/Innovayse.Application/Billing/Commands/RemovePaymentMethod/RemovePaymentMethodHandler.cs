namespace Innovayse.Application.Billing.Commands.RemovePaymentMethod;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Clients.Common;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="RemovePaymentMethodCommand"/>.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="stripe">Stripe service for detaching the payment method.</param>
/// <param name="caller">Who is asking; the command does not say, and must not.</param>
public sealed class RemovePaymentMethodHandler(
    IClientRepository clientRepo,
    IStripeService stripe,
    ICurrentRequestContext caller)
{
    /// <summary>
    /// Detaches the given payment method from the client's Stripe Customer.
    /// </summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ClientProfileNotFoundException">
    /// Thrown when no client record exists for the user.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the client has no saved payment method yet, or the given payment method
    /// does not belong to this client.
    /// </exception>
    public async Task HandleAsync(RemovePaymentMethodCommand cmd, CancellationToken ct)
    {
        var userId = caller.RequireUserId();

        var client = await clientRepo.FindByUserIdAsync(userId, ct)
            ?? throw new ClientProfileNotFoundException(userId);

        if (client.StripeCustomerId is null)
        {
            throw new InvalidOperationException("This account has no saved payment methods.");
        }

        // Detach takes a bare payment method ID with no customer scoping, so without this
        // check a client could remove a payment method that belongs to somebody else's
        // Stripe Customer just by knowing (or guessing) its ID.
        var methods = await stripe.ListPaymentMethodsAsync(client.StripeCustomerId, ct);
        if (!methods.Any(m => m.Id == cmd.PaymentMethodId))
        {
            throw new InvalidOperationException("That payment method does not belong to this account.");
        }

        await stripe.DetachPaymentMethodAsync(cmd.PaymentMethodId, ct);
    }
}
