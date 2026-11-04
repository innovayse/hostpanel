using Innovayse.Hostpanel.Domain.Settings.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>
/// Describes one configuration field for an integration or plugin.
/// Returned by the detail endpoint so the admin panel can render the form dynamically.
/// </summary>
/// <param name="Key">Storage key used in the Settings table (e.g. "secret_key").</param>
/// <param name="Label">Human-readable label shown above the field (e.g. "Secret Key").</param>
/// <param name="Type">Input kind of the field; a secret kind is rendered as a write-only box.</param>
/// <param name="Required">Whether the field must be non-empty before the integration can be enabled.</param>
/// <param name="Options">Allowed values for "select" type fields; null for all other types.</param>
/// <param name="Min">Smallest accepted value of a number field; null for no lower bound or any other type.</param>
/// <param name="Max">Largest accepted value of a number field; null for no upper bound or any other type.</param>
public sealed record FieldDefinitionDto(
    string Key,
    string Label,
    IntegrationFieldType Type,
    bool Required,
    IReadOnlyList<string>? Options = null,
    decimal? Min = null,
    decimal? Max = null);
