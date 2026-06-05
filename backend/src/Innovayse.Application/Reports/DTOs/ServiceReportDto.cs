namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row in the Services report.</summary>
public record ServiceReportDto(
    int Id,
    int ClientId,
    string ClientName,
    int ProductId,
    string ProductName,
    string? Domain,
    string BillingCycle,
    decimal FirstPaymentAmount,
    decimal RecurringAmount,
    string? PaymentMethod,
    string RegistrationDate,
    string? NextDueDate,
    string? TerminatedAt,
    string Status,
    string? Notes);

/// <summary>Paginated result for the Services report.</summary>
public record ServiceReportResultDto(IReadOnlyList<ServiceReportDto> Items, int TotalCount);
