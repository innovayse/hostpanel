namespace Innovayse.Application.Billing.Queries.ListAvailablePaymentMethods;

/// <summary>
/// One payment method offered at checkout.
/// </summary>
/// <param name="Module">Module id the checkout sends back as the order's payment method.</param>
/// <param name="DisplayName">Label the payer sees.</param>
public record AvailablePaymentMethodDto(string Module, string DisplayName);
