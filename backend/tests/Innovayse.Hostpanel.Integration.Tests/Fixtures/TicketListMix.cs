namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The tickets one seeded mix holds, by the role each plays in the ticket list filter tests. They are listed newest
/// first as delta, gamma, beta, alpha, so the expected order of a page is known.
/// </summary>
/// <param name="Token">The text every seeded subject carries, so a search scopes a request to this mix.</param>
/// <param name="ClientId">The client every ticket of the mix belongs to.</param>
/// <param name="DepartmentOne">The department of alpha and beta.</param>
/// <param name="DepartmentTwo">The department of gamma and delta.</param>
/// <param name="Alpha">An open, low priority ticket of department one, the oldest.</param>
/// <param name="Beta">A closed, high priority ticket of department one.</param>
/// <param name="Gamma">An open, high priority, flagged ticket of department two.</param>
/// <param name="Delta">A medium priority ticket of department two that awaits the client's reply, the newest.</param>
public sealed record TicketListMix(
    string Token, int ClientId, int DepartmentOne, int DepartmentTwo, int Alpha, int Beta, int Gamma, int Delta);
