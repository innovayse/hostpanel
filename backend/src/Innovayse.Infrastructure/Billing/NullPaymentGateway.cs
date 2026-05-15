namespace Innovayse.Infrastructure.Billing;

using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// No-op payment gateway that always succeeds.
/// Used in development and testing until a real gateway (Stripe) is wired up.
/// </summary>
/// <param name="logger">Logger for recording simulated charge operations.</param>
public sealed class NullPaymentGateway(ILogger<NullPaymentGateway> logger) : IPaymentGateway
{
    /// <inheritdoc/>
    public Task<PaymentResult> ChargeAsync(ChargeRequest request, CancellationToken ct)
    {
        var transactionId = $"null-{request.InvoiceId}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        logger.LogInformation(
            "NullPaymentGateway: charged {Amount} {Currency} for client {ClientId}, invoice {InvoiceId}. TxId={TransactionId}",
            request.Amount, request.Currency, request.ClientId, request.InvoiceId, transactionId);

        return Task.FromResult(new PaymentResult(true, transactionId, null));
    }
}
