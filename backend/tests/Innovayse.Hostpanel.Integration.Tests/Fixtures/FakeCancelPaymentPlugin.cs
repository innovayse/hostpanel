using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Payment;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IPaymentCancelPlugin"/> a test drives: it registers sessions like <see cref="FakePaymentPlugin"/> and
/// records every order the host asks it to cancel, answering with a set outcome or throwing, as a gateway outage would.
/// A fake port standing in for a hosted gateway that can void an order, which a test must never actually call.
/// </summary>
public sealed class FakeCancelPaymentPlugin : IPaymentCancelPlugin
{
    /// <summary>Gets the gateway orders the host asked to cancel, in order.</summary>
    public List<string> CancelledOrders { get; } = [];

    /// <summary>Gets or sets the outcome <see cref="CancelPaymentAsync"/> answers with.</summary>
    public PaymentCancelOutcome Outcome { get; set; } = PaymentCancelOutcome.Cancelled;

    /// <summary>Gets or sets an exception <see cref="CancelPaymentAsync"/> throws instead of answering, when set.</summary>
    public Exception? CancelThrows { get; set; }

    /// <summary>Gets or sets the order id the next <see cref="CreatePaymentAsync"/> answers with.</summary>
    public string NextOrderId { get; set; } = "gw-cancel-fake";

    /// <inheritdoc/>
    public string CurrencyCode => "840";

    /// <inheritdoc/>
    public Task<PaymentSession> CreatePaymentAsync(PaymentInstruction instruction, CancellationToken ct)
    {
        return Task.FromResult(new PaymentSession(NextOrderId, $"https://pg.example/pay?mdOrder={NextOrderId}"));
    }

    /// <inheritdoc/>
    public Task<GatewayPaymentStatus> GetStatusAsync(string gatewayOrderId, CancellationToken ct)
    {
        return Task.FromResult(new GatewayPaymentStatus(GatewayPaymentState.Declined, null, "orderStatus:6"));
    }

    /// <inheritdoc/>
    public Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct)
    {
        throw new NotSupportedException("No test exercising this fake needs a refund.");
    }

    /// <inheritdoc/>
    public Task<PaymentCancelResult> CancelPaymentAsync(string gatewayOrderId, CancellationToken ct)
    {
        CancelledOrders.Add(gatewayOrderId);
        return CancelThrows is not null
            ? Task.FromException<PaymentCancelResult>(CancelThrows)
            : Task.FromResult(new PaymentCancelResult(Outcome, null));
    }
}
