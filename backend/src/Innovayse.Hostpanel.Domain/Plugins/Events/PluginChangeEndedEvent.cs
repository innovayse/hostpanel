using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Plugins.Events;

/// <summary>
/// Domain event raised when a plugin change that the host applied in the background has ended: the restart that applied it
/// has loaded the new version, or could not. Admins are told, because nobody waits on a restart to see how it went. A change
/// applied inside an admin's own request is answered to that admin and raises no event.
/// </summary>
/// <param name="PluginId">The id of the plugin that was changed, cleaned of control characters because it comes from an uploaded manifest.</param>
/// <param name="FromVersion">The version that ran before the change; <see langword="null"/> for a new install.</param>
/// <param name="ToVersion">The version the change brought.</param>
/// <param name="Applied"><see langword="true"/> when the new version is loaded and running; <see langword="false"/> when it could not be loaded.</param>
/// <param name="Error">Why the new version could not be loaded; <see langword="null"/> when it was loaded.</param>
/// <param name="RestoredVersion">The version that was restored after a failed change; <see langword="null"/> when none was (a new install is left uninstalled) and when the change succeeded.</param>
/// <param name="OccurredAt">When the change ended, UTC, as the host recorded it; the same instant for every delivery of the event.</param>
public sealed record PluginChangeEndedEvent(
    string PluginId,
    string? FromVersion,
    string ToVersion,
    bool Applied,
    string? Error,
    string? RestoredVersion,
    DateTimeOffset OccurredAt) : IDomainEvent;
