using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Servers.Events;

/// <summary>
/// Raised when a new provisioning server is added to the system.
/// </summary>
/// <param name="Name">Display name of the new server.</param>
/// <param name="Module">Control panel module of the new server.</param>
public record ServerCreatedEvent(string Name, ServerModule Module) : IDomainEvent
{
    /// <summary>
    /// When the change was made, UTC, given by whoever raised the event.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
