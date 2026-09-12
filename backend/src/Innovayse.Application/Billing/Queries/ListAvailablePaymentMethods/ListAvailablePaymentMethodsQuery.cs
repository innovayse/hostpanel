namespace Innovayse.Application.Billing.Queries.ListAvailablePaymentMethods;

/// <summary>
/// Query for the payment methods a payer can pick at checkout right now.
/// </summary>
/// <remarks>
/// Carries nothing: availability is a property of the deployment and the admin's integration
/// switches, not of who is asking.
/// </remarks>
public record ListAvailablePaymentMethodsQuery();
