using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// Card gateway for the public demo: reports itself configured and settles every payment intent it opened.
/// Saved cards are not here: the demo gateway module answers them through <see cref="DemoPaymentPlugin"/>'s stored
/// credential capability. Identifiers are derived from the input, never random, and nothing leaves the process.
/// </summary>
public sealed class DemoCardPaymentGateway : ICardPaymentGateway
{
    /// <summary>Remembers the payment intents the demo opened.</summary>
    private readonly DemoCardState _state;

    /// <summary>Initializes the gateway.</summary>
    /// <param name="state">Remembers the payment intents the demo opened.</param>
    public DemoCardPaymentGateway(DemoCardState state)
    {
        _state = state;
    }

    /// <inheritdoc/>
    public bool IsConfigured => true;

    /// <inheritdoc/>
    /// <remarks>The intent is remembered (in <see cref="DemoCardState"/>) as settled, so verifying it reports what it was opened for.</remarks>
    public Task<CardPaymentIntentResult> CreatePaymentIntentAsync(
        CardPaymentIntentInstruction instruction, CancellationToken ct)
    {
        var currency = instruction.Currency.ToLowerInvariant();
        var intentId = $"demo-intent-{instruction.InvoiceId}-{currency}";
        _state.OpenIntent(intentId, new CardPaymentIntentStatus(
            true,
            $"demo-txn-{intentId}",
            CurrencyCode.ToMinorUnits(instruction.Amount, instruction.Currency),
            currency,
            instruction.OrderId,
            instruction.InvoiceId));
        return Task.FromResult(new CardPaymentIntentResult(intentId));
    }

    /// <inheritdoc/>
    /// <remarks>An intent the demo never opened reads as unsettled, for no amount.</remarks>
    public Task<CardPaymentIntentStatus> VerifyPaymentIntentAsync(CardPaymentVerifyInstruction instruction, CancellationToken ct) =>
        Task.FromResult(_state.FindIntent(instruction.PaymentIntentId)
            ?? new CardPaymentIntentStatus(false, null, 0, string.Empty, null, null));

    /// <inheritdoc/>
    /// <remarks>Every refund succeeds with an id derived from the transaction.</remarks>
    public Task<CardRefundResult> RefundAsync(CardRefundInstruction instruction, CancellationToken ct) =>
        Task.FromResult(new CardRefundResult(true, $"demo-refund-{instruction.TransactionId}"));
}
