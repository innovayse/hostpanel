namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>An account holder registered for a <see cref="PluginUiFixture"/> test, with a client of the fixture's host.</summary>
/// <param name="Owner">The registered account holder, signed in against the shared factory.</param>
/// <param name="Http">A client of the fixture's plugin host carrying the account holder's bearer token.</param>
public sealed record PluginUiOwner(SignedInClient Owner, HttpClient Http);
