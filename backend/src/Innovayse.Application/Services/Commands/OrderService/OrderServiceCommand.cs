namespace Innovayse.Application.Services.Commands.OrderService;

/// <summary>Command to order a new service for a client.</summary>
/// <param name="ClientId">The client placing the order.</param>
/// <param name="ProductId">The product being ordered.</param>
/// <param name="BillingCycle">Billing cycle: "monthly" or "annual".</param>
public record OrderServiceCommand(int ClientId, int ProductId, string BillingCycle);
