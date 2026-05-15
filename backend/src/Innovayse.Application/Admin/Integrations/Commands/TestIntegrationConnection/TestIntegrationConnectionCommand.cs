namespace Innovayse.Application.Admin.Integrations.Commands.TestIntegrationConnection;

/// <summary>
/// Command that validates whether all required credentials are configured for an integration.
/// Does not make any live network call -- confirms local configuration completeness only.
/// </summary>
/// <param name="Slug">URL-safe integration identifier, e.g. "cpanel".</param>
public record TestIntegrationConnectionCommand(string Slug);
