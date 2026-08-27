namespace Innovayse.API.Billing.Requests;

using System.Text.Json.Serialization;

/// <summary>
/// Body for <c>PUT api/me/payment-methods/{id}</c>.
/// </summary>
/// <remarks>
/// The only update Stripe actually allows on an existing card is which one is the default --
/// its number, brand and expiry cannot be changed once issued. <see cref="SetAsDefault"/> is
/// therefore the only field this accepts; a request that omits it is refused rather than
/// silently accepted and ignored.
/// </remarks>
public sealed class UpdateMyPaymentMethodRequest
{
    /// <summary>Whether to make this the account's default payment method.</summary>
    [JsonPropertyName("set_as_default")]
    public bool SetAsDefault { get; set; }
}
