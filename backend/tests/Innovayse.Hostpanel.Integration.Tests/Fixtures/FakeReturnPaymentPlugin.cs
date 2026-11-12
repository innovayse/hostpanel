using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Payment;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IPaymentReturnPlugin"/> a test drives: it answers a set session and a set status, records every completion
/// from a return request with the parameters it was handed, and the order its calls came in, so a test can prove the host
/// completes before it reads the status. It also takes a cancel, to prove a refused session is voided. A fake port standing in
/// for a hosted gateway, which a test must never actually call.
/// </summary>
public sealed class FakeReturnPaymentPlugin : IPaymentReturnPlugin, IPaymentCancelPlugin
{
    /// <summary>Gets what each completion was handed, in order.</summary>
    public List<(string GatewayOrderId, IReadOnlyDictionary<string, string> Parameters)> Completions { get; } = [];

    /// <summary>Gets the name of each call the host made (<c>complete</c>, <c>status</c>), in order.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>Gets the gateway orders the host asked to cancel, in order.</summary>
    public List<string> CancelledOrders { get; } = [];

    /// <summary>Gets or sets the session <see cref="CreatePaymentAsync"/> answers with.</summary>
    public PaymentSession CreatePaymentResult { get; set; } =
        new("gw-step", "https://pg.example/step", null, PaymentSessionKind.BrowserStep);

    /// <summary>Gets or sets the status <see cref="GetStatusAsync"/> answers with.</summary>
    public GatewayPaymentStatus StatusResult { get; set; } = new(GatewayPaymentState.Pending, null, "pending");

    /// <summary>Gets or sets an exception <see cref="CompleteAsync"/> throws instead of answering, when set.</summary>
    public Exception? CompleteThrows { get; set; }

    /// <inheritdoc/>
    public string CurrencyCode => "840";

    /// <inheritdoc/>
    public Task<PaymentSession> CreatePaymentAsync(PaymentInstruction instruction, CancellationToken ct)
    {
        return Task.FromResult(CreatePaymentResult);
    }

    /// <inheritdoc/>
    public Task<GatewayPaymentStatus> GetStatusAsync(string gatewayOrderId, CancellationToken ct)
    {
        Calls.Add("status");
        return Task.FromResult(StatusResult);
    }

    /// <inheritdoc/>
    public Task<GatewayPaymentStatus> CompleteAsync(
        string gatewayOrderId, IReadOnlyDictionary<string, string> returnParameters, CancellationToken ct)
    {
        Calls.Add("complete");
        Completions.Add((gatewayOrderId, returnParameters));
        return CompleteThrows is not null
            ? Task.FromException<GatewayPaymentStatus>(CompleteThrows)
            : Task.FromResult(StatusResult);
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
        return Task.FromResult(new PaymentCancelResult(PaymentCancelOutcome.Cancelled, null));
    }
}
