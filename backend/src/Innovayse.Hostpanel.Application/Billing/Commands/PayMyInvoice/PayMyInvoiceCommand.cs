namespace Innovayse.Hostpanel.Application.Billing.Commands.PayMyInvoice;

/// <summary>Command for a client to pay one of their own invoices with a payment method saved on their account.</summary>
/// <remarks>
/// Carries no client id. Which account the invoice and the payment method must belong to is
/// resolved inside the handler from the credential. It carries no currency either: the invoice is
/// charged in the currency it was issued in.
/// </remarks>
/// <param name="InvoiceId">The invoice to pay; the controller overlays it from the route.</param>
/// <param name="GatewayModuleId">
/// The payment gateway module that holds the method. Nullable for the same reason as
/// <paramref name="PaymentMethodId"/>; the validator requires it.
/// </param>
/// <param name="PaymentMethodId">
/// The gateway's reference for the saved method to charge. It must be one saved on the caller's own account.
/// Nullable because a non-nullable member of a bound body is implicitly required, which would refuse
/// a missing id with a framework sentence instead of the validator's.
/// </param>
/// <param name="AttemptId">
/// The payment attempt, chosen by the client once per pay action and sent again unchanged when the
/// same action is retried. The charge is deduplicated at the gateway by it, so a retried request is
/// one charge, while a new pay action (after a decline, say) is a new charge. Nullable for the same
/// reason as <paramref name="PaymentMethodId"/>; the validator requires it.
/// </param>
public sealed record PayMyInvoiceCommand(int InvoiceId, string? GatewayModuleId, string? PaymentMethodId, Guid? AttemptId);
