namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>The input of <see cref="Innovayse.Hostpanel.Application.Common.Interfaces.IPluginPackageStore.InstallAsync"/>: one uploaded plugin archive.</summary>
/// <param name="Content">The uploaded ZIP archive; the caller owns and disposes it. Read once, front to back.</param>
/// <param name="AllowUnsigned">
/// Whether the admin setting "allow unsigned plugins" is on, read by the caller at the moment of the upload. It
/// matters only to a host that enforces signatures; a development host accepts an unsigned package either way.
/// </param>
public sealed record PluginPackageUpload(Stream Content, bool AllowUnsigned);
