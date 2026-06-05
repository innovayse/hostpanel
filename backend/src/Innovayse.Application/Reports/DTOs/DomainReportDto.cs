namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row in the Domains report.</summary>
public record DomainReportDto(
    int Id,
    int ClientId,
    string ClientName,
    int? OrderId,
    string OrderType,
    string DomainName,
    decimal FirstPaymentAmount,
    decimal RecurringAmount,
    int RegistrationPeriod,
    string RegistrationDate,
    string ExpiryDate,
    string NextDueDate,
    string? Registrar,
    string? PaymentMethod,
    string Status,
    string? Notes);

/// <summary>Paginated result for the Domains report.</summary>
public record DomainReportResultDto(IReadOnlyList<DomainReportDto> Items, int TotalCount);
