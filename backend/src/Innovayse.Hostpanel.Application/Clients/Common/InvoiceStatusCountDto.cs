using Innovayse.Hostpanel.Application.Billing.Currencies.Common;

namespace Innovayse.Hostpanel.Application.Clients.Common;

/// <summary>Invoice count and per-currency totals for a single status.</summary>
/// <param name="Count">Number of invoices in this status, in every currency.</param>
/// <param name="Totals">Sum of the invoice totals in this status, one entry per currency the client has such invoices in; empty when there are none.</param>
public sealed record InvoiceStatusCountDto(int Count, IReadOnlyList<CurrencyAmountDto> Totals);
