namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// Result returned by install and remove plugin operations.
/// The client should display a "restart required" banner when <see cref="RequiresRestart"/> is true.
/// </summary>
/// <param name="RequiresRestart">
/// True when the change takes effect only after a server restart, because the running process could not apply it; false
/// when it is already applied (<paramref name="Reloaded"/>), and when a removal found the files already gone and only
/// switched the plugin off and queued the purge of its data.
/// </param>
/// <param name="StagedVersion">
/// The version of an upgrade (or forced downgrade) that was staged and replaces the installed plugin at the next
/// restart; <see langword="null"/> for every other result, and when the upgrade was applied without a restart.
/// </param>
/// <param name="Reloaded">
/// True when the running process applied the change itself: the new version is loaded, or the removed plugin is unloaded,
/// with no restart.
/// </param>
/// <param name="Restarting">True when the host has started to restart to apply the change: it stops taking new requests, lets the running ones finish and exits for the restart policy, so the panel waits for it to return instead of asking the admin to restart.</param>
public sealed record PluginActionResultDto(bool RequiresRestart, string? StagedVersion = null, bool Reloaded = false, bool Restarting = false);
