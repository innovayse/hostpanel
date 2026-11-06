namespace Innovayse.Hostpanel.Application.Reports.Common;

/// <summary>The Sales Tax Liability totals of one invoice currency.</summary>
/// <param name="Currency">The ISO 4217 code the amounts are in.</param>
/// <param name="TotalInvoices">The number of invoices paid in the period in this currency.</param>
/// <param name="TotalInvoiced">The sum of their totals.</param>
/// <param name="TaxLiability">The sum of the tax they carried, less the tax of the account credit notes issued in the period.</param>
public sealed record SalesTaxCurrencyTotalDto(
    string Currency,
    int TotalInvoices,
    decimal TotalInvoiced,
    decimal TaxLiability);
