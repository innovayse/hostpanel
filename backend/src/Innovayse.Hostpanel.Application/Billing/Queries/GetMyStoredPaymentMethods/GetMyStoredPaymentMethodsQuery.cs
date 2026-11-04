namespace Innovayse.Hostpanel.Application.Billing.Queries.GetMyStoredPaymentMethods;

/// <summary>Query to list the calling client's saved payment methods: one entry per active charge consent.</summary>
/// <remarks>
/// Carries no client id. Which account is resolved inside the handler from the credential.
/// </remarks>
public sealed record GetMyStoredPaymentMethodsQuery();
