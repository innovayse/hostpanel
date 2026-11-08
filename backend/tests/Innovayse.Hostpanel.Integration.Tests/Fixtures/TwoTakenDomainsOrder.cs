namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>A Pending order whose two lines each register a domain the registrar refuses.</summary>
/// <param name="OrderId">The order's id.</param>
/// <param name="ClientId">The ordering client.</param>
/// <param name="FirstDomain">The domain of the first line.</param>
/// <param name="SecondDomain">The domain of the second line.</param>
public sealed record TwoTakenDomainsOrder(int OrderId, int ClientId, string FirstDomain, string SecondDomain);
