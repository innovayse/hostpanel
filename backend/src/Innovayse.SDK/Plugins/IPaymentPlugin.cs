namespace Innovayse.SDK.Plugins;

/// <summary>
/// Contract for redirect-based payment gateway plugins (hosted payment page model).
/// Implement via <see cref="Base.PaymentGatewayBase"/>.
/// </summary>
public interface IPaymentPlugin
{
    /// <summary>
    /// Gets the ISO 4217 numeric currency code (e.g. "840" for USD, "051" for AMD) that this
    /// plugin instance will charge in, as configured by the admin. Callers must verify this
    /// matches the currency of the invoice being paid before calling <see cref="CreatePaymentAsync"/> —
    /// the gateway itself has no notion of the panel's billing currency and will happily charge
    /// the requested minor-unit amount in whatever currency it is configured for.
    /// </summary>
    string CurrencyCode { get; }

    /// <summary>Registers a payment at the gateway and returns the redirect session.</summary>
    /// <param name="request">Order number, amount in minor units, return URL and optional metadata.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The gateway-side order id and the URL to redirect the payer to.</returns>
    Task<PaymentSession> CreatePaymentAsync(PaymentRequest request, CancellationToken ct);

    /// <summary>Queries the gateway for the current state of a previously created payment.</summary>
    /// <param name="gatewayOrderId">The gateway-side order id returned by <see cref="CreatePaymentAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The mapped payment state with the gateway transaction reference when paid.</returns>
    Task<GatewayPaymentStatus> GetStatusAsync(string gatewayOrderId, CancellationToken ct);

    /// <summary>Refunds a captured payment, fully or partially.</summary>
    /// <param name="gatewayOrderId">The gateway-side order id of the paid payment.</param>
    /// <param name="amountMinor">Refund amount in minor currency units.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A gateway reference for the refund operation.</returns>
    Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct);
}
