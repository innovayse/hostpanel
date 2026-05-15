namespace Innovayse.Domain.Servers.Events;
using Innovayse.Domain.Common;

/// <summary>
/// Raised when a new provisioning server is added to the system.
/// </summary>
/// <param name="Name">Display name of the new server.</param>
/// <param name="Module">Control panel module of the new server.</param>
public record ServerCreatedEvent(string Name, ServerModule Module) : IDomainEvent;
