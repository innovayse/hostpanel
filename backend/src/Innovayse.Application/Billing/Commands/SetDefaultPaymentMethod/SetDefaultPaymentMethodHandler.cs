namespace Innovayse.Application.Billing.Commands.SetDefaultPaymentMethod;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="SetDefaultPaymentMethodCommand"/>.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="stripe">Stripe service for updating the customer's default payment method.</param>
public sealed class SetDefaultPaymentMethodHandler(IClientRepository clientRepo, IStripeService stripe)
{
    /// <summary>
    /// Makes the given payment method the client's default.
    /// </summary>
    /// <param name="cmd">The command containing the user's Identity ID and target payment method.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no client record exists for the user, or the client has no saved payment
    /// method yet.
    /// </exception>
    public async Task HandleAsync(SetDefaultPaymentMethodCommand cmd, CancellationToken ct)
    {
        var client = await clientRepo.FindByUserIdAsync(cmd.UserId, ct)
            ?? throw new InvalidOperationException($"No client profile found for user {cmd.UserId}.");

        if (client.StripeCustomerId is null)
        {
            throw new InvalidOperationException("This account has no saved payment methods.");
        }

        // Stripe's own API would reject a payment method that isn't attached to this customer,
        // but checking here first means a mismatched ID gets this handler's own error rather
        // than an opaque Stripe API exception -- and it keeps the same check in place if that
        // Stripe-side validation ever turns out to be less strict than assumed.
        var methods = await stripe.ListPaymentMethodsAsync(client.StripeCustomerId, ct);
        if (!methods.Any(m => m.Id == cmd.PaymentMethodId))
        {
            throw new InvalidOperationException("That payment method does not belong to this account.");
        }

        await stripe.SetDefaultPaymentMethodAsync(client.StripeCustomerId, cmd.PaymentMethodId, ct);
    }
}
