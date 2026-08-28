namespace Innovayse.Application.Billing.Queries.GetMyPaymentMethods;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Clients.Common;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="GetMyPaymentMethodsQuery"/>.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="stripe">Stripe service for reading saved payment methods.</param>
/// <param name="caller">Who is asking; the query does not say, and must not.</param>
public sealed class GetMyPaymentMethodsHandler(
    IClientRepository clientRepo,
    IStripeService stripe,
    ICurrentRequestContext caller)
{
    /// <summary>
    /// Retrieves the saved payment methods for the authenticated client.
    /// </summary>
    /// <param name="query">The query. It names no account: this reads the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The client's saved cards and bank accounts, or an empty list for a client who has
    /// never had one attached -- there is nothing to list, not an error.
    /// </returns>
    /// <exception cref="ClientProfileNotFoundException">
    /// Thrown when no client record exists for the user. Carries the user id for the log only --
    /// the API answers 404 with a code and an identifier-free sentence.
    /// </exception>
    public async Task<IReadOnlyList<StripePaymentMethodDto>> HandleAsync(
        GetMyPaymentMethodsQuery query, CancellationToken ct)
    {
        var userId = caller.RequireUserId();

        var client = await clientRepo.FindByUserIdAsync(userId, ct)
            ?? throw new ClientProfileNotFoundException(userId);

        return client.StripeCustomerId is null
            ? []
            : await stripe.ListPaymentMethodsAsync(client.StripeCustomerId, ct);
    }
}
