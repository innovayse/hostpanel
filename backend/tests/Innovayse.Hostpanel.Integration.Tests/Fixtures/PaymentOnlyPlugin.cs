using Hostpanel.Sdk.Plugins.Payment;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Shows only the payment side of another plugin: every call goes to <paramref name="inner"/>, but the object itself
/// implements <see cref="IPaymentPlugin"/> and nothing else, so the host sees a gateway that offers no stored
/// credentials even though the plugin behind it could. A fake port standing in for such a gateway, which a test must
/// never actually call.
/// </summary>
/// <param name="inner">The plugin the calls are handed to.</param>
public sealed class PaymentOnlyPlugin(IPaymentPlugin inner) : IPaymentPlugin
{
    /// <inheritdoc/>
    public string CurrencyCode => inner.CurrencyCode;

    /// <inheritdoc/>
    public Task<PaymentSession> CreatePaymentAsync(PaymentInstruction instruction, CancellationToken ct) =>
        inner.CreatePaymentAsync(instruction, ct);

    /// <inheritdoc/>
    public Task<GatewayPaymentStatus> GetStatusAsync(string gatewayOrderId, CancellationToken ct) =>
        inner.GetStatusAsync(gatewayOrderId, ct);

    /// <inheritdoc/>
    public Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct) =>
        inner.RefundAsync(gatewayOrderId, amountMinor, ct);
}
