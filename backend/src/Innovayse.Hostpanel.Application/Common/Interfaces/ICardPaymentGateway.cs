using Innovayse.Hostpanel.Application.Billing.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The built-in card gateway's one-off payments: an order paid by card at checkout, the check that the payment
/// really succeeded, and its refund. Saved cards and off-session charges are not here: they go through the
/// stored credential capability of the payment gateway module (<see cref="IStoredCredentialPluginResolver"/>), so
/// any gateway plugin can offer them.
/// </summary>
public interface ICardPaymentGateway
{
    /// <summary>Gets whether the deployment carries the gateway's credentials; without them no call may be made.</summary>
    bool IsConfigured { get; }

    /// <summary>Opens a payment intent the browser confirms to pay an order.</summary>
    /// <param name="instruction">What to charge and for which order.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The client secret the card form confirms the intent with, or why the gateway refused.</returns>
    Task<CardPaymentIntentResult> CreatePaymentIntentAsync(CardPaymentIntentInstruction instruction, CancellationToken ct);

    /// <summary>Reads a payment intent back from the gateway, so a payment is never believed on the browser's word.</summary>
    /// <param name="instruction">The intent to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the intent succeeded, with the amount and currency the gateway took.</returns>
    Task<CardPaymentIntentStatus> VerifyPaymentIntentAsync(CardPaymentVerifyInstruction instruction, CancellationToken ct);

    /// <summary>Refunds a card payment.</summary>
    /// <param name="instruction">The payment to refund and how much.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The refund id, or why the gateway refused.</returns>
    Task<CardRefundResult> RefundAsync(CardRefundInstruction instruction, CancellationToken ct);
}
