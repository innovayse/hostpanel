namespace Innovayse.Application.Services.Queries.GetCancellationStatus;

/// <summary>DTO indicating whether a service has a pending cancellation request.</summary>
/// <param name="Pending"><see langword="true"/> if an open cancellation request exists.</param>
/// <param name="Type">The cancellation type if pending, or <see langword="null"/>.</param>
public record CancellationStatusDto(bool Pending, string? Type);
