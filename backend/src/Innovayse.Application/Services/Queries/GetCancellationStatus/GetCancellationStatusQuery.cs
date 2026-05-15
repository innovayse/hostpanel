namespace Innovayse.Application.Services.Queries.GetCancellationStatus;

/// <summary>Checks whether a service has a pending cancellation request.</summary>
/// <param name="ServiceId">The client service primary key.</param>
public record GetCancellationStatusQuery(int ServiceId);
