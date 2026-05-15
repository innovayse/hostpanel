namespace Innovayse.Domain.Billing;

/// <summary>Result of a <see cref="Interfaces.IPaymentGateway.ChargeAsync"/> call.</summary>
/// <param name="Success">Whether the charge succeeded.</param>
/// <param name="TransactionId">Gateway transaction reference; non-null when <see cref="Success"/> is true.</param>
/// <param name="ErrorMessage">Human-readable error; non-null when <see cref="Success"/> is false.</param>
public record PaymentResult(bool Success, string? TransactionId, string? ErrorMessage);
