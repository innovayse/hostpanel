namespace Innovayse.Hostpanel.Application.Admin.Integrations.Commands.TestIntegrationConnection;

/// <summary>
/// Command that tests an integration: a built-in one by its required fields and, where it has one, a live probe; a
/// plugin by asking its <c>IPluginStatus</c> within the host's time limit.
/// </summary>
/// <param name="Slug">URL-safe integration identifier, e.g. "panel".</param>
public sealed record TestIntegrationConnectionCommand(string Slug);
