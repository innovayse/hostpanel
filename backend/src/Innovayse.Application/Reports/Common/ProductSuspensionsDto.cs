namespace Innovayse.Application.Reports.Common;

/// <summary>One row in the Product Suspensions report.</summary>
public record ProductSuspensionRowDto(
    int ServiceId,
    string ClientName,
    string ProductName,
    string? Domain,
    string? NextDueDate,
    string? SuspendReason);
