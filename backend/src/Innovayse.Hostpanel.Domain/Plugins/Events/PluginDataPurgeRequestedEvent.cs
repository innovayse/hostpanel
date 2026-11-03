using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Plugins.Events;

/// <summary>
/// Domain event raised when an operator asks to purge the stored data a removed plugin kept. It travels through the
/// outbox in the same transaction as the request's audit line, so the data is purged if and only if the request
/// committed, and a crash after the commit still purges it once the durable queue recovers the message.
/// </summary>
/// <param name="PluginId">The id of the plugin whose data is purged, already checked against the plugin id rule.</param>
/// <param name="OccurredAt">When the operator asked, UTC, as the caller of the domain method read it; rows written later are kept.</param>
public sealed record PluginDataPurgeRequestedEvent(string PluginId, DateTimeOffset OccurredAt) : IDomainEvent;
