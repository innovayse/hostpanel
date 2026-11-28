using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Services.Events;

/// <summary>Raised when a free trial is converted into a paid service and awaits its first payment.</summary>
/// <param name="ServiceId">The trial service ID.</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="TrialEndsAt">When the trial ends, which is when the paid period starts, UTC.</param>
/// <param name="PaymentDueAt">The moment the first invoice must be paid by, UTC.</param>
/// <param name="OccurredAt">When the conversion began, UTC, as the caller read it.</param>
public record TrialConvertedEvent(int ServiceId, int ClientId, DateTimeOffset TrialEndsAt, DateTimeOffset PaymentDueAt, DateTimeOffset OccurredAt) : IDomainEvent;
