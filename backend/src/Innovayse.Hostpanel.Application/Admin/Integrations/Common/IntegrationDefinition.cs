namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>
/// What an integration slug declares, from the built-in catalogue or the loaded plugin's manifest:
/// the name the admin sees and the fields its form renders.
/// </summary>
/// <param name="Name">Display name of the integration: an admin locale key for a built-in one, the manifest name for a plugin.</param>
/// <param name="Fields">Ordered field definitions the admin form renders.</param>
public sealed record IntegrationDefinition(
    string Name,
    IReadOnlyList<FieldDefinitionDto> Fields);
