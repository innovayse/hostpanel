using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.InstallPlugin;

/// <summary>
/// Installs a plugin from a ZIP archive uploaded by the admin, or upgrades the installed plugin of the same id: a
/// different version is staged and replaces the installed one at the next restart.
/// </summary>
/// <param name="Content">
/// Stream of the uploaded ZIP file, or <see langword="null"/> when the request carried no file; the
/// caller owns and disposes it.
/// </param>
/// <param name="ForceDowngrade">
/// Whether the operator accepts a version older than the installed one; without it such an upload is refused with
/// <c>PLUGIN_DOWNGRADE_REFUSED</c>.
/// </param>
public sealed record InstallPluginCommand(Stream? Content, bool ForceDowngrade = false) : IDemoRestricted;
