using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Slides.Events;

/// <summary>Raised when a new slide is created.</summary>
/// <param name="SlideId">The new slide's ID (0 until persisted).</param>
/// <param name="IconName">The MDI icon name.</param>
public record SlideCreatedEvent(int SlideId, string IconName) : IDomainEvent
{
    /// <summary>
    /// When the change was made, UTC, given by whoever raised the event.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
