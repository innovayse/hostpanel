namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the staff alert about a plugin change that the host applied in the background and that has ended.</summary>
/// <param name="PluginId">The plugin id, read as <c>{{ plugin_id }}</c>.</param>
/// <param name="FromVersion">The version that ran before the change, read as <c>{{ from_version }}</c>; empty for a new install.</param>
/// <param name="ToVersion">The version the change brought, read as <c>{{ to_version }}</c>.</param>
/// <param name="Applied">Whether the new version is loaded and running, read as <c>{{ applied }}</c>.</param>
/// <param name="Error">Why the new version could not be loaded, read as <c>{{ error }}</c>; empty when it was loaded.</param>
/// <param name="RestoredVersion">The version that was restored after a failed change, read as <c>{{ restored_version }}</c>; empty when none was.</param>
public sealed record PluginChangeEndedAdminTemplateModel(
    string PluginId,
    string? FromVersion,
    string ToVersion,
    bool Applied,
    string? Error,
    string? RestoredVersion) : EmailTemplateModel;
