using Innovayse.Hostpanel.Application.Billing.Common.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>A card gateway customer, none found, or why the gateway could not answer.</summary>
/// <param name="CustomerId">The card gateway customer ID, or <see langword="null"/> when none exists or the call failed.</param>
/// <param name="Failure">Why the gateway could not answer, or <see langword="null"/> when it did.</param>
public sealed record CardCustomerResult(string? CustomerId, CardGatewayFailure? Failure = null)
{
    /// <summary>The answer of a lookup that found no customer.</summary>
    public static CardCustomerResult None { get; } = new((string?)null);

    /// <summary>Builds the answer for a call the gateway could not serve.</summary>
    /// <param name="failure">Why the gateway could not be asked.</param>
    /// <returns>A result with no customer carrying <paramref name="failure"/>.</returns>
    public static CardCustomerResult Failed(CardGatewayFailure failure) => new(null, failure);
}
