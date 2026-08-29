namespace Innovayse.Application.Reports.Common;

/// <summary>One country row in the VAT MOSS report.</summary>
public record VatMossRowDto(
    string CountryName,
    string CountryCode,
    decimal VatRate,
    int NumberOfInvoices,
    decimal TotalExclVat,
    decimal TotalVatCollected,
    string Currency);
