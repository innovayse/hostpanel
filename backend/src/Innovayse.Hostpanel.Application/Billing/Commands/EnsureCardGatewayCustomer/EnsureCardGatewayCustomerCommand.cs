namespace Innovayse.Hostpanel.Application.Billing.Commands.EnsureCardGatewayCustomer;

/// <summary>
/// Makes sure a client has exactly one card gateway customer, opening and storing it on first use, so a
/// card can be saved against it. Safe to call from any number of requests at once.
/// </summary>
/// <param name="ClientId">The client record's id.</param>
public sealed record EnsureCardGatewayCustomerCommand(int ClientId);
