namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>Names the client whose card gateway customer is looked up by the id the gateway keeps on it.</summary>
/// <param name="ClientId">The client the customer was opened for.</param>
public sealed record CardFindCustomerInstruction(int ClientId);
