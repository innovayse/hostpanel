namespace Innovayse.API.Services;

/// <summary>Request body for ordering a new service.</summary>
/// <param name="ProductId">The product to order.</param>
/// <param name="BillingCycle">Billing cycle: "monthly" or "annual".</param>
public record OrderRequest(int ProductId, string BillingCycle);
