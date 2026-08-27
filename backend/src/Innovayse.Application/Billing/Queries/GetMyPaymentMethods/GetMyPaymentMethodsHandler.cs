namespace Innovayse.Application.Billing.Queries.GetMyPaymentMethods;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="GetMyPaymentMethodsQuery"/>.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="stripe">Stripe service for reading saved payment methods.</param>
public sealed class GetMyPaymentMethodsHandler(IClientRepository clientRepo, IStripeService stripe)
{
    /// <summary>
    /// Retrieves the saved payment methods for the authenticated client.
    /// </summary>
    /// <param name="query">The query containing the user's Identity ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The client's saved cards and bank accounts, or an empty list for a client who has
    /// never had one attached -- there is nothing to list, not an error.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when no client record exists for the user.</exception>
    public async Task<IReadOnlyList<StripePaymentMethodDto>> HandleAsync(
        GetMyPaymentMethodsQuery query, CancellationToken ct)
    {
        var client = await clientRepo.FindByUserIdAsync(query.UserId, ct)
            ?? throw new InvalidOperationException($"No client profile found for user {query.UserId}.");

        return client.StripeCustomerId is null
            ? []
            : await stripe.ListPaymentMethodsAsync(client.StripeCustomerId, ct);
    }
}
