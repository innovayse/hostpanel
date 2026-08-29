namespace Innovayse.Application.Services.Queries.ListCancellationRequests;

/// <summary>DTO representing a cancellation request with enriched service and client data.</summary>
/// <param name="Id">Primary key of the cancellation request.</param>
/// <param name="ServiceId">FK to the client service being cancelled.</param>
/// <param name="ServiceName">Product name from the linked service.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Full name of the owning client.</param>
/// <param name="Type">
/// The <see cref="Innovayse.Domain.Services.CancellationType"/> member name -- <c>Immediate</c>
/// or <c>EndOfBillingPeriod</c>. A machine-readable value, not display text: the caller owns the
/// wording, in its own language.
/// </param>
/// <param name="Reason">Optional client-supplied reason.</param>
/// <param name="Status">
/// The <see cref="Innovayse.Domain.Services.CancellationStatus"/> member name -- <c>Open</c> or
/// <c>Closed</c>. Machine-readable for the same reason as <paramref name="Type"/>.
/// </param>
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
