namespace Innovayse.Application.Reports.Common;

/// <summary>Full Sales Tax Liability report result.</summary>
public record SalesTaxReportDto(
    int TotalInvoices,
    decimal TotalInvoiced,
    decimal TaxLevel1Liability,
    decimal TaxLevel2Liability,
    IReadOnlyList<SalesTaxRowDto> Rows);
