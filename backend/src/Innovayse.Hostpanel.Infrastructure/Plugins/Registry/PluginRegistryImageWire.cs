namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>The registry's JSON for an icon or a screenshot of a plugin version.</summary>
/// <param name="Url">The image address, absolute or relative to the registry address.</param>
/// <param name="ContentType">The image media type.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
internal sealed record PluginRegistryImageWire(string? Url, string? ContentType, int? Width, int? Height);
