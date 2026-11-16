namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The orders one seeded mix holds, by the role each plays in the order list tests. They were placed in the order
/// alpha, beta, gamma, delta, so a list shows them newest first as delta, gamma, beta, alpha.
/// </summary>
/// <param name="Token">The text every seeded order number starts with, so a search scopes a request to this mix.</param>
/// <param name="Alpha">A pending order, the oldest; its number ends with ALPHA.</param>
/// <param name="Beta">A cancelled order; its number ends with BETA.</param>
/// <param name="Gamma">An active order; its number ends with GAMMA.</param>
/// <param name="Delta">A pending order, the newest; its number ends with DELTA.</param>
public sealed record OrderListMix(string Token, int Alpha, int Beta, int Gamma, int Delta);
