namespace Innovayse.Application.Reports.DTOs;

/// <summary>One country row in the VAT MOSS report.</summary>
public record VatMossRowDto(
    string CountryName,
    string CountryCode,
    decimal VatRate,
    int NumberOfInvoices,
    decimal TotalExclVat,
    decimal TotalVatCollected,
    string Currency);

/// <summary>VAT MOSS Settlement Data report result.</summary>
public record VatMossDto(
    string PeriodLabel,
    IReadOnlyList<VatMossRowDto> Rows);
