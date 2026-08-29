namespace Innovayse.Application.Billing.Commands.SetDefaultPaymentMethod;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Clients.Common;
using Innovayse.Application.Common;
using Innovayse.Application.Resources;
using Innovayse.Domain.Clients.Interfaces;
using Microsoft.Extensions.Localization;

/// <summary>
/// Handles <see cref="SetDefaultPaymentMethodCommand"/>.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="stripe">Stripe service for updating the customer's default payment method.</param>
/// <param name="caller">Who is asking; the command does not say, and must not.</param>
/// <param name="localizer">The refusal sentences, in the caller's own language.</param>
public sealed class SetDefaultPaymentMethodHandler(
    IClientRepository clientRepo,
    IStripeService stripe,
    ICurrentRequestContext caller,
    IStringLocalizer<ValidationMessages> localizer)
{
    /// <summary>
    /// Makes the given payment method the client's default.
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
    public async Task HandleAsync(SetDefaultPaymentMethodCommand cmd, CancellationToken ct)
    {
        var userId = caller.RequireUserId();

        var client = await clientRepo.FindByUserIdAsync(userId, ct)
            ?? throw new ClientProfileNotFoundException(userId);

        if (client.StripeCustomerId is null)
        {
            throw new InvalidOperationException(localizer["NoSavedPaymentMethods"]);
        }

        // Stripe's own API would reject a payment method that isn't attached to this customer,
        // but checking here first means a mismatched ID gets this handler's own error rather
        // than an opaque Stripe API exception -- and it keeps the same check in place if that
        // Stripe-side validation ever turns out to be less strict than assumed.
        var methods = await stripe.ListPaymentMethodsAsync(client.StripeCustomerId, ct);
        if (!methods.Any(m => m.Id == cmd.PaymentMethodId))
        {
            throw new InvalidOperationException(localizer["PaymentMethodNotOwned"]);
        }

        await stripe.SetDefaultPaymentMethodAsync(client.StripeCustomerId, cmd.PaymentMethodId, ct);
    }
}
