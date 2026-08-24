namespace Innovayse.Application.Billing.Commands.ReconcileGatewayPaymentsCron;

/// <summary>
/// Recurring job that completes hosted-gateway payments whose payer never returned
/// to the site (the gateway has no webhooks). Self-reschedules every 5 minutes.
/// </summary>
public sealed record ReconcileGatewayPaymentsCronCommand;
