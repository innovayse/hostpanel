using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Services.Events;

/// <summary>Raised when a reminder that a free trial is about to end is recorded.</summary>
/// <param name="ServiceId">The trial service ID.</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="DaysLeft">The reminder day: how many days before the end the reminder goes out.</param>
/// <param name="TrialEndsAt">When the trial ends, UTC.</param>
/// <param name="OccurredAt">When the reminder was recorded, UTC, as the caller read it.</param>
public record TrialEndingSoonEvent(int ServiceId, int ClientId, int DaysLeft, DateTimeOffset TrialEndsAt, DateTimeOffset OccurredAt) : IDomainEvent;
