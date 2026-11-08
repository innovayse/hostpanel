namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>The registry's JSON for a plugin page.</summary>
/// <param name="Summary">The listing row.</param>
/// <param name="Licence">The licence name.</param>
/// <param name="Repository">The source repository address.</param>
/// <param name="BuyUrl">Where to buy the plugin.</param>
/// <param name="ContactUrl">Where to contact the publisher.</param>
/// <param name="Latest">The newest published version.</param>
/// <param name="DocumentationUrl">The documentation address.</param>
/// <param name="SupportUrl">The support address.</param>
internal sealed record PluginRegistryDetailWire(
    PluginRegistryEntryWire? Summary,
    string? Licence,
    string? Repository,
    string? BuyUrl,
    string? ContactUrl,
    PluginRegistryVersionWire? Latest,
    string? DocumentationUrl = null,
    string? SupportUrl = null);
