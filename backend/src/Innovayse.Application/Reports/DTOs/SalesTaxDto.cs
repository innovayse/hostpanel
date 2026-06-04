namespace Innovayse.Application.Reports.DTOs;

/// <summary>One invoice row in the Sales Tax Liability report.</summary>
public record SalesTaxRowDto(
    int Id,
    string ClientName,
    string InvoiceDate,
    string? DatePaid,
    decimal SubTotal,
    decimal Tax,
    decimal Credit,
    decimal Total);

/// <summary>Full Sales Tax Liability report result.</summary>
public record SalesTaxReportDto(
    int TotalInvoices,
    decimal TotalInvoiced,
    decimal TaxLevel1Liability,
    decimal TaxLevel2Liability,
    IReadOnlyList<SalesTaxRowDto> Rows);
