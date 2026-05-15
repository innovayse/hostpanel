namespace Innovayse.Domain.Billing;

/// <summary>Input for <see cref="Interfaces.IPaymentGateway.ChargeAsync"/>.</summary>
/// <param name="ClientId">The client being charged.</param>
/// <param name="InvoiceId">The invoice being paid.</param>
/// <param name="Amount">Charge amount.</param>
/// <param name="Currency">ISO 4217 currency code (e.g. "USD").</param>
public record ChargeRequest(int ClientId, int InvoiceId, decimal Amount, string Currency);
