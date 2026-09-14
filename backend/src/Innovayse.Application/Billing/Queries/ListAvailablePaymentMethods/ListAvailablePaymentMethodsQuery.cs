namespace Innovayse.Application.Billing.Queries.ListAvailablePaymentMethods;

/// <summary>
/// Query for the payment methods a payer can pick at checkout right now.
/// </summary>
/// <remarks>
/// Availability is a property of the deployment, the admin's integration switches and the
/// currency the payer is billed in. By default that currency is the caller's own. The order
/// flow names it instead, because it checks a method for a guest whose client — and so whose
/// currency — does not exist yet at the moment the caller is asked about.
/// </remarks>
/// <param name="CurrencyCode">
/// ISO 4217 alpha code to list for, any case; <see langword="null"/> to list for the caller's
/// own currency. A code the panel does not offer lists nothing.
/// </param>
public record ListAvailablePaymentMethodsQuery(string? CurrencyCode = null);
