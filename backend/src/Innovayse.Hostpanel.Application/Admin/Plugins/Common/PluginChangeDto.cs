using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>The last change made to an installed plugin and how it ended, as the plugin manager shows it.</summary>
/// <param name="PluginId">The plugin id.</param>
/// <param name="FromVersion">The version that ran before the change; <see langword="null"/> for a new install.</param>
/// <param name="ToVersion">The version the change brought.</param>
/// <param name="Result">Whether the new version loaded.</param>
/// <param name="Error">Why the new version did not load; <see langword="null"/> when it did.</param>
/// <param name="RestoredVersion">The version that was restored after a failed change; <see langword="null"/> when none was (a new install is left uninstalled) and when the change succeeded.</param>
/// <param name="At">When the change ended.</param>
public sealed record PluginChangeDto(
    string PluginId,
    string? FromVersion,
    string ToVersion,
    PluginChangeResult Result,
    string? Error,
    string? RestoredVersion,
    DateTimeOffset At);
