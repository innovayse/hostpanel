namespace Innovayse.Hostpanel.Application.Billing.Commands.ReconcileInvoicePaymentClaims;

/// <summary>
/// One pass over the stored charge claims that have stayed unresolved for longer than they should: asks each claim's
/// payment plugin whether its idempotency key ever charged and applies the answer. A charge that went through is
/// recorded even if the consent was revoked or the invoice changed in between, because the pass runs the key lookup
/// without any of the preconditions a new charge needs. It is a step of the gateway reconciliation job, dispatched by
/// it, not a job of its own.
/// </summary>
public sealed record ReconcileInvoicePaymentClaimsCommand;
