namespace Innovayse.Hostpanel.Application.Billing.Commands.EnsureGatewayCustomer;

/// <summary>
/// Makes sure a client has exactly one account at a payment gateway module, opening and storing it on first use, so
/// a card can be saved against it. Safe to call from any number of requests at once.
/// </summary>
/// <param name="ClientId">The client record's id.</param>
/// <param name="GatewayModuleId">The payment gateway module the account is with (for example "stripe").</param>
public sealed record EnsureGatewayCustomerCommand(int ClientId, string GatewayModuleId);
