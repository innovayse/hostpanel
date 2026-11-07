namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// The icon a loaded plugin ships, read and checked when the plugin was loaded (see the icon rules of the manifest
/// validation), so serving it reads no file.
/// </summary>
/// <param name="ContentType">The media type of the bytes: <c>image/svg+xml</c> or <c>image/png</c>.</param>
/// <param name="Content">The checked bytes of the icon file.</param>
/// <param name="ETag">The strong entity tag of the bytes, quoted, for conditional requests.</param>
public sealed record PluginIconDto(string ContentType, byte[] Content, string ETag);
