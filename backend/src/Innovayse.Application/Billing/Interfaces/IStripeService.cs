namespace Innovayse.Application.Billing.Interfaces;

/// <summary>Abstraction over Stripe payment operations. Implemented in Infrastructure.</summary>
public interface IStripeService
{
    /// <summary>
    /// Creates a Stripe PaymentIntent for the specified amount and currency.
    /// </summary>
    /// <param name="amount">The payment amount in the major currency unit (e.g. dollars).</param>
    /// <param name="currency">ISO 4217 currency code (e.g. "usd").</param>
    /// <param name="metadata">Key-value metadata to attach to the PaymentIntent.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The PaymentIntent client secret used by the frontend to confirm payment.</returns>
    Task<string> CreatePaymentIntentAsync(decimal amount, string currency, Dictionary<string, string> metadata, CancellationToken ct);

    /// <summary>
    /// Verifies that a PaymentIntent has succeeded.
    /// </summary>
    /// <param name="paymentIntentId">The Stripe PaymentIntent identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple indicating success and the transaction ID (charge ID or intent ID).</returns>
    Task<(bool Success, string? TransactionId)> VerifyPaymentIntentAsync(string paymentIntentId, CancellationToken ct);

    /// <summary>
    /// Issues a full refund for a previously completed payment.
    /// Accepts either a Stripe charge ID (<c>ch_xxx</c>) or a PaymentIntent ID (<c>pi_xxx</c>).
    /// </summary>
    /// <param name="transactionId">The Stripe charge ID or PaymentIntent ID to refund.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The Stripe refund ID (<c>re_xxx</c>).</returns>
    /// <exception cref="System.Exception">Thrown when the payment gateway rejects the refund request.</exception>
    Task<string> RefundAsync(string transactionId, CancellationToken ct);
}
