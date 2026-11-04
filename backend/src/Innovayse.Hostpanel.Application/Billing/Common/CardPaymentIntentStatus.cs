using Innovayse.Hostpanel.Application.Billing.Common.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>What the card gateway reports about a payment intent: whether it settled, for how much, and what it was opened for.</summary>
/// <remarks>
/// The amount, currency and ids come from the gateway's own record of the intent, so a caller can
/// check that the intent it is handed really pays the invoice it claims to pay.
/// </remarks>
/// <param name="Succeeded"><see langword="true"/> only when the gateway settled the payment.</param>
/// <param name="TransactionId">The charge ID, or the intent ID when there is no charge; set when <paramref name="Succeeded"/> is true.</param>
/// <param name="AmountMinor">The amount the intent was opened for, in the currency's minor unit.</param>
/// <param name="Currency">ISO 4217 code of the intent's currency.</param>
/// <param name="OrderId">The order the intent was opened for, or <see langword="null"/> when it carries none.</param>
/// <param name="InvoiceId">The invoice the intent was opened for, or <see langword="null"/> when it carries none.</param>
/// <param name="Failure">
/// Why the gateway could not be asked: <see cref="CardGatewayFailure.Unavailable"/> when it could not
/// be reached, so nothing is known about the intent, or <see cref="CardGatewayFailure.Refused"/> when
/// it refused to read it; <see langword="null"/> when the gateway answered.
/// </param>
public sealed record CardPaymentIntentStatus(
    bool Succeeded,
    string? TransactionId,
    long AmountMinor,
    string Currency,
    int? OrderId,
    int? InvoiceId,
    CardGatewayFailure? Failure = null);
