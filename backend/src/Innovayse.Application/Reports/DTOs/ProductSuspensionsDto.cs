namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row in the Product Suspensions report.</summary>
public record ProductSuspensionRowDto(
    int ServiceId,
    string ClientName,
    string ProductName,
    string? Domain,
    string? NextDueDate,
    string? SuspendReason);
