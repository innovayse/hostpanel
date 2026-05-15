namespace Innovayse.Application.Services.DTOs;

/// <summary>DTO representing a cancellation request with enriched service and client data.</summary>
/// <param name="Id">Primary key of the cancellation request.</param>
/// <param name="ServiceId">FK to the client service being cancelled.</param>
/// <param name="ServiceName">Product name from the linked service.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Full name of the owning client.</param>
/// <param name="Type">Display string for the cancellation type ("Immediate" or "End of Billing Period").</param>
/// <param name="Reason">Optional client-supplied reason.</param>
/// <param name="Status">Display string for the request status ("Open" or "Closed").</param>
/// <param name="CreatedAt">UTC timestamp when the request was submitted.</param>
public record CancellationRequestDto(
    int Id,
    int ServiceId,
    string ServiceName,
    int ClientId,
    string ClientName,
    string Type,
    string? Reason,
    string Status,
    DateTimeOffset CreatedAt);
