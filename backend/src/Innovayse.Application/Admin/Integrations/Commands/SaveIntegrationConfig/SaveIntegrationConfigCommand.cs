namespace Innovayse.Application.Admin.Integrations.Commands.SaveIntegrationConfig;

/// <summary>
/// Command that upserts all configuration settings for one integration.
/// </summary>
/// <param name="Slug">URL-safe integration identifier, e.g. "stripe".</param>
/// <param name="IsEnabled">Whether the integration should be active after saving.</param>
/// <param name="Config">
/// Dictionary of field key to value to persist.
/// Fields whose value equals the mask placeholder ("••••••••") are skipped so stored
/// secrets are not overwritten when the admin re-saves without changing them.
/// </param>
public record SaveIntegrationConfigCommand(
    string Slug,
    bool IsEnabled,
    Dictionary<string, string> Config);
