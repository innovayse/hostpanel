namespace Innovayse.SDK.Base;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base class for all payment gateway plugins.
/// Provides <see cref="GetConfig"/> and a structured <see cref="Logger"/>.
/// </summary>
public abstract class PaymentGatewayBase(
    string pluginId,
    IConfiguration configuration,
    ILogger logger) : IPaymentPlugin
{
    /// <summary>Gets the structured logger pre-injected for this provider.</summary>
    protected ILogger Logger { get; } = logger;

    /// <summary>
    /// Reads a configuration value from the Settings table via the standard key
    /// <c>integration:{pluginId}:{key}</c>.
    /// </summary>
    /// <param name="key">The field key as declared in <c>plugin.json</c>.</param>
    /// <returns>The stored value, or <see langword="null"/> if not set.</returns>
    protected string? GetConfig(string key)
        => configuration[$"integration:{pluginId}:{key}"];

    /// <summary>Registers a payment at the gateway and returns the redirect session.</summary>
    /// <param name="request">Order number, amount in minor units, return URL and optional metadata.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The gateway-side order id and the URL to redirect the payer to.</returns>
    public abstract Task<PaymentSession> CreatePaymentAsync(PaymentRequest request, CancellationToken ct);

    /// <summary>Queries the gateway for the current state of a previously created payment.</summary>
    /// <param name="gatewayOrderId">The gateway-side order id returned by <see cref="CreatePaymentAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The mapped payment state with the gateway transaction reference when paid.</returns>
    public abstract Task<GatewayPaymentStatus> GetStatusAsync(string gatewayOrderId, CancellationToken ct);

    /// <summary>Refunds a captured payment, fully or partially.</summary>
    /// <param name="gatewayOrderId">The gateway-side order id of the paid payment.</param>
    /// <param name="amountMinor">Refund amount in minor currency units.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A gateway reference for the refund operation.</returns>
    public abstract Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct);
}
