namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>An image of a plugin version (icon or screenshot), always at an address on the registry's own origin.</summary>
/// <param name="Url">The absolute image address on the registry's origin.</param>
/// <param name="ContentType">The image media type, cleaned of control characters.</param>
/// <param name="Width">The width in pixels, when the registry states it.</param>
/// <param name="Height">The height in pixels, when the registry states it.</param>
public sealed record RegistryPluginImage(string Url, string ContentType, int? Width, int? Height);
