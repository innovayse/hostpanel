namespace Innovayse.Application.Services.Commands.OrderService;

/// <summary>Command to order a new service for a client.</summary>
/// <param name="ClientId">The client placing the order.</param>
/// <param name="ProductId">The product being ordered.</param>
/// <param name="BillingCycle">Billing cycle: "monthly" or "annual".</param>
/// <param name="FirstPaymentAmount">One-time amount charged on the first invoice.</param>
/// <param name="RecurringAmount">Amount charged on each renewal invoice.</param>
/// <param name="PaymentMethod">Payment method used (e.g. "Stripe", "BankTransfer").</param>
/// <param name="Domain">Domain name to pre-fill, when purchased alongside hosting.</param>
public record OrderServiceCommand(int ClientId, int ProductId, string BillingCycle,
    decimal FirstPaymentAmount, decimal RecurringAmount, string? PaymentMethod = null,
    string? Domain = null);
