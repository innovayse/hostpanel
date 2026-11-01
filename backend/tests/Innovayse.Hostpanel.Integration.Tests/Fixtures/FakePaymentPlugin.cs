using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Payment;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IPaymentPlugin"/> a test drives entirely: what it charges in, what it hands
/// back from <see cref="CreatePaymentAsync"/> and <see cref="GetStatusAsync"/>, and, when a test
/// needs to prove a call never happened, what it throws instead. A fake port standing in for a
/// hosted bank gateway, which a test must never actually call.
/// </summary>
public sealed class FakePaymentPlugin : IPaymentPlugin
{
    /// <summary>How many times <see cref="CreatePaymentAsync"/> was called.</summary>
    public int CreatePaymentCallCount { get; private set; }

    /// <summary>How many times <see cref="GetStatusAsync"/> was called.</summary>
    public int GetStatusCallCount { get; private set; }

    /// <summary>The instruction the last <see cref="CreatePaymentAsync"/> call carried.</summary>
    public PaymentInstruction? LastCreatePaymentInstruction { get; private set; }

    /// <inheritdoc/>
    public string CurrencyCode { get; set; } = "840";

    /// <summary>The session <see cref="CreatePaymentAsync"/> answers with.</summary>
    public PaymentSession CreatePaymentResult { get; set; } = new("gw-fake", "https://pg.example/pay?mdOrder=gw-fake");

    /// <summary>An exception <see cref="CreatePaymentAsync"/> throws instead of answering, as a gateway outage would, when set.</summary>
    public Exception? CreatePaymentThrows { get; set; }

    /// <summary>The status <see cref="GetStatusAsync"/> answers with.</summary>
    public GatewayPaymentStatus StatusResult { get; set; } = new(GatewayPaymentState.Pending, null, "orderStatus:0");

    /// <summary>An exception <see cref="GetStatusAsync"/> throws instead of answering, when set.</summary>
    public Exception? StatusThrows { get; set; }

    /// <summary>
    /// The refund id <see cref="RefundAsync"/> answers with when a test sets it; unset, it throws,
    /// for every test that never expects a refund call.
    /// </summary>
    public string? RefundResult { get; set; }

    /// <summary>The gateway order id the last <see cref="RefundAsync"/> call carried.</summary>
    public string? LastRefundGatewayOrderId { get; private set; }

    /// <summary>The minor-unit amount the last <see cref="RefundAsync"/> call carried.</summary>
    public long? LastRefundAmountMinor { get; private set; }

    /// <inheritdoc/>
    public Task<PaymentSession> CreatePaymentAsync(PaymentInstruction instruction, CancellationToken ct)
    {
        CreatePaymentCallCount++;
        LastCreatePaymentInstruction = instruction;
        return CreatePaymentThrows is not null
            ? Task.FromException<PaymentSession>(CreatePaymentThrows)
            : Task.FromResult(CreatePaymentResult);
    }

    /// <inheritdoc/>
    public Task<GatewayPaymentStatus> GetStatusAsync(string gatewayOrderId, CancellationToken ct)
    {
        GetStatusCallCount++;
        return StatusThrows is not null
            ? Task.FromException<GatewayPaymentStatus>(StatusThrows)
            : Task.FromResult(StatusResult);
    }

    /// <inheritdoc/>
    public Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct)
    {
        LastRefundGatewayOrderId = gatewayOrderId;
        LastRefundAmountMinor = amountMinor;
        return RefundResult is not null
            ? Task.FromResult(RefundResult)
            : throw new NotSupportedException("No test exercising this fake needs a refund.");
    }
}
