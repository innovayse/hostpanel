namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A stub fulfilment plugin a <see cref="FulfilmentScenario"/> enabled, and the id it is enabled under.
/// </summary>
/// <param name="PluginId">The plugin id its records name and its client references are computed for.</param>
/// <param name="Plugin">The stub that records what it is handed.</param>
public sealed record EnabledStubPlugin(string PluginId, StubFulfilmentPlugin Plugin);
