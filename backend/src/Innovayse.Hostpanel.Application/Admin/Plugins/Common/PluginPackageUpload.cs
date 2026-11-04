namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>The input of <see cref="Innovayse.Hostpanel.Application.Common.Interfaces.IPluginPackageStore.InstallAsync"/>: one uploaded plugin archive.</summary>
/// <param name="Content">The uploaded ZIP archive; the caller owns and disposes it. Read once, front to back.</param>
/// <param name="AllowUnsigned">
/// Whether the admin setting "allow unsigned plugins" is on, read by the caller at the moment of the upload. It
/// matters only to a host that enforces signatures; a development host accepts an unsigned package either way.
/// </param>
/// <param name="ForceDowngrade">
/// Whether the operator accepts an upload older than the installed version. Without it a downgrade is refused with
/// <see cref="Enums.PluginPackageOutcome.DowngradeRefused"/>.
/// </param>
/// <param name="RequireSignature">
/// Whether the package must carry a valid signature by a trusted key even on a host that does not enforce signatures
/// (Development, Testing). A package fetched from the plugin registry always sets it; an upload by hand does not.
/// </param>
public sealed record PluginPackageUpload(Stream Content, bool AllowUnsigned, bool ForceDowngrade = false, bool RequireSignature = false);
