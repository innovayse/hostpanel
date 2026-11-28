using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Services.Events;

/// <summary>Raised when a running free trial is terminated instead of becoming a paid service.</summary>
/// <param name="ServiceId">The trial service ID.</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="TrialEndsAt">When the trial ended, UTC: its end, or the termination when that came earlier.</param>
/// <param name="OccurredAt">When the trial was terminated, UTC, as the caller read it.</param>
public record TrialEndedEvent(int ServiceId, int ClientId, DateTimeOffset TrialEndsAt, DateTimeOffset OccurredAt) : IDomainEvent;
