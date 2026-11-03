using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Plugins.Events;

/// <summary>
/// Domain event raised when an administrator removes an uploaded plugin and its switch is turned off. It travels
/// through the outbox in the same transaction as the switch, so the plugin's stored data is purged if and only if the
/// removal committed, and a crash after the commit still purges it once the durable queue recovers the message.
/// </summary>
/// <param name="PluginId">The id of the removed plugin, already checked against the plugin id rule.</param>
/// <param name="OccurredAt">When the removal was made, UTC, as the caller of the domain method read it.</param>
public sealed record PluginRemovedEvent(string PluginId, DateTimeOffset OccurredAt) : IDomainEvent;
