namespace Innovayse.Application.Admin.Integrations.DTOs;

/// <summary>
/// Describes one configuration field for an integration or plugin.
/// Returned by the detail endpoint so the admin panel can render the form dynamically.
/// </summary>
/// <param name="Key">Storage key used in the Settings table (e.g. "secret_key").</param>
/// <param name="Label">Human-readable label shown above the field (e.g. "Secret Key").</param>
/// <param name="Type">Input type — "text", "password", "select", or "textarea".</param>
/// <param name="Required">Whether the field must be non-empty before the integration can be enabled.</param>
/// <param name="Options">Allowed values for "select" type fields; null for all other types.</param>
public record FieldDefinitionDto(
    string Key,
    string Label,
    string Type,
    bool Required,
    IReadOnlyList<string>? Options = null);
