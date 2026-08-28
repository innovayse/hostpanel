namespace Innovayse.Application.Clients.Queries.ExportClientData;

/// <summary>One admin action, as the <c>activityLog</c> section of a client data export lists it.</summary>
/// <param name="Id">The log entry's primary key.</param>
/// <param name="Description">What was done to the account.</param>
/// <param name="CreatedAt">When it was done.</param>
/// <param name="AdminName">Staff member who did it, or null when the platform acted on its own.</param>
public sealed record ClientExportActivityDto(
    int Id,
    string Description,
    DateTimeOffset CreatedAt,
    string? AdminName);
