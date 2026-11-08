namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// The registry's full page for one plugin. The links are external web addresses (http or https) kept as given, not
/// restricted to the registry's origin.
/// </summary>
/// <param name="Summary">The listing row (installed state not yet set); its <c>LatestVersion</c> is empty when nothing is published.</param>
/// <param name="Licence">The licence name, when stated.</param>
/// <param name="Repository">The source repository address, when stated.</param>
/// <param name="BuyUrl">Where to buy a paid plugin, when stated.</param>
/// <param name="ContactUrl">Where to contact the publisher, when stated.</param>
/// <param name="DocumentationUrl">The documentation address, when stated.</param>
/// <param name="SupportUrl">The support address, when stated.</param>
/// <param name="Latest">The newest published version, or <see langword="null"/> when there is none.</param>
public sealed record RegistryPluginDetails(
    RegistryPluginDto Summary,
    string? Licence,
    string? Repository,
    string? BuyUrl,
    string? ContactUrl,
    string? DocumentationUrl,
    string? SupportUrl,
    RegistryPluginVersion? Latest);
