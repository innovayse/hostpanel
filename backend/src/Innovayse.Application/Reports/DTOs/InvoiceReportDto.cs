namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row of the Invoices report — mirrors WHMCS fields.</summary>
public record InvoiceReportRowDto(
    int Id,
    int ClientId,
    string ClientName,
    string? InvoiceNumber,
    string CreatedDate,
    string DueDate,
    string? DatePaid,
    string? DateRefunded,
    string? DateCancelled,
    decimal SubTotal,
    decimal Credit,
    decimal Tax,
    decimal TaxRate,
    decimal Total,
    string Status,
    string? PaymentMethod,
    string? Notes);

/// <summary>Paginated result for the Invoices report.</summary>
public record InvoiceReportResultDto(
    IReadOnlyList<InvoiceReportRowDto> Items,
    int TotalCount);
