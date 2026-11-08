using Hostpanel.Sdk.Plugins.Payment;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A payment plugin whose refund call stops at a gate: it reports that the refund has started and waits until the test lets
/// it finish, so a test can act on an order while its refund is under way. A fake port standing in for a gateway; nothing
/// else of the plugin is used by the tests that hold it.
/// </summary>
public sealed class GatedRefundPlugin : IPaymentPlugin
{
    /// <summary>The longest the plugin waits for the test to release it, so a broken test fails instead of hanging.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    /// <summary>Gets the signal that the refund call has been reached.</summary>
    public TaskCompletionSource RefundStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the signal the test sends to let the refund call return.</summary>
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public string CurrencyCode => "840";

    /// <inheritdoc/>
    public Task<PaymentSession> CreatePaymentAsync(PaymentInstruction instruction, CancellationToken ct)
        => throw new NotSupportedException("No test holding this plugin starts a payment.");

    /// <inheritdoc/>
    public Task<GatewayPaymentStatus> GetStatusAsync(string gatewayOrderId, CancellationToken ct)
        => throw new NotSupportedException("No test holding this plugin reads a payment status.");

    /// <inheritdoc/>
    public async Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct)
    {
        RefundStarted.TrySetResult();
        await Release.Task.WaitAsync(Patience, ct);
        return "gated-refund-1";
    }
}
