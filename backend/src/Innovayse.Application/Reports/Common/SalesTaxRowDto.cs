namespace Innovayse.Application.Reports.Common;

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
