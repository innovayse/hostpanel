namespace Innovayse.Application.Admin.Integrations.DTOs;

/// <summary>
/// Full configuration for one integration, with secret fields masked.
/// </summary>
/// <param name="Slug">URL-safe identifier for the integration.</param>
/// <param name="IsEnabled">Whether the integration is currently active.</param>
/// <param name="Config">
/// Read-only dictionary mapping field key (e.g. "secret_key") to its stored value.
/// Any field whose key contains "key", "secret", "password", or "token" is
/// returned as "••••••••" when non-empty, or "" when empty.
/// </param>
/// <param name="FieldDefinitions">
/// Ordered list of field metadata used by the admin panel to render the config form.
/// </param>
public record IntegrationDetailDto(
    string Slug,
    bool IsEnabled,
    IReadOnlyDictionary<string, string> Config,
    IReadOnlyList<FieldDefinitionDto> FieldDefinitions);
