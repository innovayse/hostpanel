using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Plugins.Events;

/// <summary>
/// Domain event raised when an operator removes a plugin, in the save that switches it off. It travels through the
/// outbox in the same transaction, so whatever the host granted the plugin (today the SSH keys issued to it, #758) is
/// taken back if and only if the removal committed. Its stored data is not touched: that is
/// <see cref="PluginDataPurgeRequestedEvent"/>, an operator action of its own.
/// </summary>
/// <param name="PluginId">The id of the removed plugin, already checked against the plugin id shape.</param>
/// <param name="OccurredAt">When the plugin was removed, UTC, as the caller of the domain method read it.</param>
public sealed record PluginRemovedEvent(string PluginId, DateTimeOffset OccurredAt) : IDomainEvent;
