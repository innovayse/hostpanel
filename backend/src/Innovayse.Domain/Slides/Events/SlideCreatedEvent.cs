namespace Innovayse.Domain.Slides.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a new slide is created.</summary>
/// <param name="SlideId">The new slide's ID (0 until persisted).</param>
/// <param name="IconName">The MDI icon name.</param>
public record SlideCreatedEvent(int SlideId, string IconName) : IDomainEvent;
