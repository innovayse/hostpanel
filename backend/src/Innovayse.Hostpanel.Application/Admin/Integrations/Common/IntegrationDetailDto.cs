namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>
/// Full configuration for one integration. A secret field never carries its value.
/// </summary>
/// <param name="Slug">URL-safe identifier for the integration.</param>
/// <param name="Name">Display name from the built-in catalogue or the plugin manifest.</param>
/// <param name="IsEnabled">Whether the integration is currently active.</param>
/// <param name="Config">
/// Field key (e.g. "secret_key") to its stored state. A secret field, as classified by
/// <c>SettingSecrecy</c>, answers only <c>isSecret</c> and <c>hasValue</c>; its value is <c>null</c>.
/// </param>
/// <param name="FieldDefinitions">
/// Ordered list of field metadata used by the admin panel to render the config form.
/// </param>
/// <param name="CanTestConnection">
/// Whether Test Connection can check this integration: a built-in one the test knows, a payment plugin (probed through
/// its gateway status call) or a plugin that implements <c>IPluginStatus</c>. The panel hides the button otherwise.
/// </param>
/// <param name="Connection">
/// The last known connection state of a plugin that implements <c>IPluginStatus</c>; <see langword="null"/> for a
/// built-in integration and any other plugin.
/// </param>
/// <param name="IsPlugin">True when the integration comes from an installed plugin (not built-in).</param>
/// <param name="IconUrl">
/// Path of the admin icon endpoint for a plugin integration that ships an icon; <see langword="null"/> for a built-in
/// integration and for a plugin without an icon.
/// </param>
public sealed record IntegrationDetailDto(
    string Slug,
    string Name,
    bool IsEnabled,
    IReadOnlyDictionary<string, IntegrationFieldValueDto> Config,
    IReadOnlyList<FieldDefinitionDto> FieldDefinitions,
    bool CanTestConnection,
    IntegrationConnectionDto? Connection,
    bool IsPlugin = false,
    string? IconUrl = null);
