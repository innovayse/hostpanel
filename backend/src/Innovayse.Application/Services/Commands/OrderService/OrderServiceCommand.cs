namespace Innovayse.Application.Services.Commands.OrderService;

/// <summary>Command to order a new service for a client.</summary>
/// <param name="ClientId">The client placing the order.</param>
/// <param name="ProductId">The product being ordered.</param>
/// <param name="BillingCycle">Billing cycle: "monthly" or "annual".</param>
/// <param name="FirstPaymentAmount">
/// One-time amount charged on the first invoice. <see langword="null"/> means "read it from the
/// product's price in the client's currency"; <c>0</c> is a genuinely free first period (a
/// promotion, a free tier) as snapshotted on the order line, and is honoured as such.
/// </param>
/// <param name="RecurringAmount">
/// Amount charged on each renewal invoice. <see langword="null"/> means "read it from the
/// product's price in the client's currency"; <c>0</c> is a genuinely free renewal.
/// </param>
/// <param name="PaymentMethod">Payment method used (e.g. "Stripe", "BankTransfer").</param>
/// <param name="Domain">Domain name to pre-fill, when purchased alongside hosting.</param>
public record OrderServiceCommand(int ClientId, int ProductId, string BillingCycle,
    decimal? FirstPaymentAmount, decimal? RecurringAmount, string? PaymentMethod = null,
    string? Domain = null);
