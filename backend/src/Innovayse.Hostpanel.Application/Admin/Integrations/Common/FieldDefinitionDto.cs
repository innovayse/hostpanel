using Innovayse.Hostpanel.Domain.Settings.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>
/// Describes one configuration field for an integration or plugin.
/// Returned by the detail endpoint so the admin panel can render the form dynamically.
/// </summary>
/// <param name="Key">Storage key used in the Settings table (e.g. "secret_key").</param>
/// <param name="Label">Human-readable label shown above the field (e.g. "Secret Key"), in the request's language for a plugin setting.</param>
/// <param name="Type">Input kind of the field; a secret kind is rendered as a write-only box.</param>
/// <param name="Required">Whether the field must be non-empty before the integration can be enabled.</param>
/// <param name="Options">Allowed values for "select" type fields; null for all other types.</param>
/// <param name="Min">Smallest accepted value of a number field; null for no lower bound or any other type.</param>
/// <param name="Max">Largest accepted value of a number field; null for no upper bound or any other type.</param>
/// <param name="Description">Help text shown under the field, in the request's language; null for none.</param>
/// <param name="Placeholder">Hint shown in the empty input, in the request's language; null for none.</param>
/// <param name="OptionLabels">
/// The text shown for each value of <paramref name="Options"/>, keyed by the value, in the request's language; null when
/// every option is shown as its value (the built-in catalogue). The stored value is always the option's value.
/// </param>
public sealed record FieldDefinitionDto(
    string Key,
    string Label,
    IntegrationFieldType Type,
    bool Required,
    IReadOnlyList<string>? Options = null,
    decimal? Min = null,
    decimal? Max = null,
    string? Description = null,
    string? Placeholder = null,
    IReadOnlyDictionary<string, string>? OptionLabels = null);
