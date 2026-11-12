using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CompleteGatewayPayment;

/// <summary>
/// Verifies a hosted-gateway payment against the gateway and, when paid, marks the
/// invoice paid and fulfills the linked order. Idempotent: an already-paid invoice
/// returns <see cref="GatewayCompletionState.Paid"/> without touching the gateway.
/// </summary>
/// <param name="InvoiceId">The invoice whose gateway session should be verified.</param>
/// <param name="ReturnParameters">
/// The names and values of the request the payer came back from the gateway with, in the order they arrived; <see langword="null"/>
/// when the check is not a return (the reconciliation job). A return, even one that carried nothing, is handed to a plugin that
/// completes a payment from it before the status is read; a plugin that does not is only asked for the status.
/// </param>
public sealed record CompleteGatewayPaymentCommand(int InvoiceId, IReadOnlyList<PaymentReturnParameterDto>? ReturnParameters = null);
