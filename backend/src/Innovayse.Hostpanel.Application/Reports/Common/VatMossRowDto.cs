namespace Innovayse.Hostpanel.Application.Reports.Common;

/// <summary>
/// One row of the VAT MOSS report: a member state, a VAT rate and an invoice currency. The country
/// is sent as its code; the admin names it in the reader's language.
/// </summary>
/// <param name="CountryCode">The member state's ISO 3166-1 alpha-2 code.</param>
/// <param name="VatRate">The VAT rate the invoices carried, in percent.</param>
/// <param name="NumberOfInvoices">The taxed invoices paid in the quarter.</param>
/// <param name="TotalExclVat">The value invoiced excluding VAT, less the net part of the refunds made and the account credit notes issued in the quarter.</param>
/// <param name="TotalVatCollected">The VAT collected, less the VAT part of the refunds made and the account credit notes issued in the quarter.</param>
/// <param name="Currency">The ISO 4217 code of the invoices, which both totals are in.</param>
public sealed record VatMossRowDto(
    string CountryCode,
    decimal VatRate,
    int NumberOfInvoices,
    decimal TotalExclVat,
    decimal TotalVatCollected,
    string Currency);
