namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>
/// What an integration slug declares, from the built-in catalogue or the loaded plugin's manifest:
/// the name the admin sees and the fields its form renders.
/// </summary>
/// <param name="Fields">Ordered field definitions the admin form renders.</param>
/// <param name="Name">Display name of the integration: the manifest name for a plugin; left out for a built-in one, whose name the admin owns.</param>
public sealed record IntegrationDefinition(
    IReadOnlyList<FieldDefinitionDto> Fields,
    string? Name = null);
