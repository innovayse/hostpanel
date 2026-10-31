namespace Innovayse.Hostpanel.Application.Clients.Commands.LinkCardGatewayCustomer;

/// <summary>
/// Links a client to its card gateway customer, once: the first link wins and every later one answers
/// with the id that is already stored, so two requests racing to open a customer for one client end up
/// with the same one.
/// </summary>
/// <param name="ClientId">The client record's id.</param>
/// <param name="CustomerId">The card gateway customer id to store when the client has none yet.</param>
public sealed record LinkCardGatewayCustomerCommand(int ClientId, string CustomerId);
