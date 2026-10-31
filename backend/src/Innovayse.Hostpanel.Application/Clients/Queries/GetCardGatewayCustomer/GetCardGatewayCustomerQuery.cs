using Innovayse.Hostpanel.Application.Clients.Common;

namespace Innovayse.Hostpanel.Application.Clients.Queries.GetCardGatewayCustomer;

/// <summary>
/// Requests the <see cref="CardGatewayCustomerDto"/> of one client record by id, for work done with
/// nobody signed in (the renewal job), so the Billing module can reach the saved payment methods
/// without reading the Clients tables.
/// </summary>
/// <param name="ClientId">The client record's id.</param>
public sealed record GetCardGatewayCustomerQuery(int ClientId);
