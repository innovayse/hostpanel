namespace Innovayse.API.Admin.Requests;

/// <summary>Request body for saving an integration configuration.</summary>
public sealed class SaveIntegrationConfigRequest
{
    /// <summary>Gets or initializes whether the integration should be enabled after saving.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>
    /// Gets or initializes the field values to persist.
    /// Map of field key (e.g. "secret_key") to value.
    /// Secret fields received as "••••••••" are silently skipped by the handler
    /// so the stored credential is not erased when the admin re-saves the form.
    /// </summary>
    public required Dictionary<string, string> Config { get; init; }
}
