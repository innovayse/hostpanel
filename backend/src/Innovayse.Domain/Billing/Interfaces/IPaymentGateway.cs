namespace Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Abstraction over a payment gateway provider (Stripe, PayPal, etc.).
/// Implemented in Infrastructure; select via DI configuration.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>
    /// Charges the client for the given invoice.
    /// </summary>
    /// <param name="request">Charge details including amount and currency.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="PaymentResult"/> indicating success or failure.</returns>
    Task<PaymentResult> ChargeAsync(ChargeRequest request, CancellationToken ct);
}
