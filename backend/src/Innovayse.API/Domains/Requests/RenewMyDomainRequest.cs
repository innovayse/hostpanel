namespace Innovayse.API.Domains.Requests;

/// <summary>Request payload for a client renewing one of their own domain registrations.</summary>
/// <remarks>
/// Its own type rather than a reuse of <see cref="RenewDomainRequest"/>, which the admin route
/// binds. A client renewal is a purchase and has to name the gateway its invoice will be paid
/// through; the admin one calls the registrar directly and raises no invoice, so a payment
/// method there would be a field with nothing behind it.
/// </remarks>
public sealed class RenewMyDomainRequest
{
    /// <summary>Gets the number of years to extend the domain registration by (1–10).</summary>
    public required int Years { get; init; }

    /// <summary>Gets the payment gateway module the invoice will be paid through.</summary>
    public required string PaymentMethod { get; init; }
}
