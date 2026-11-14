namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The services one seeded mix holds, by the role each plays in the service list tests. They are listed newest first
/// as terminated, suspended, active, pending, so the expected order of a page is known.
/// </summary>
/// <param name="Token">The text every seeded product name and domain carries, so a search scopes a request to this mix.</param>
/// <param name="ClientId">The client every service of the mix belongs to.</param>
/// <param name="AlphaProductId">The product of the pending and the active service.</param>
/// <param name="BetaProductId">The product of the suspended and the terminated service.</param>
/// <param name="Pending">A pending, monthly service of the alpha product on a domain of its own, the oldest.</param>
/// <param name="Active">An active, annual service of the alpha product on a domain of its own.</param>
/// <param name="Suspended">A suspended, monthly service of the beta product that has no domain of its own but a linked registered domain.</param>
/// <param name="Terminated">A terminated, quarterly service of the beta product on a domain of its own, the newest.</param>
public sealed record ServiceListMix(
    string Token, int ClientId, int AlphaProductId, int BetaProductId, int Pending, int Active, int Suspended, int Terminated);
