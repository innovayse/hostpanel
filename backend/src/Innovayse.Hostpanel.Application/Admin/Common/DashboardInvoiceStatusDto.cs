using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Common;

/// <summary>The invoices raised in the dashboard period that stand in one status.</summary>
/// <param name="Status">The status; a partially refunded invoice counts as paid.</param>
/// <param name="Count">How many invoices stand in it.</param>
/// <param name="Amount">Their totals added up in the base currency.</param>
public sealed record DashboardInvoiceStatusDto(InvoiceStatus Status, int Count, decimal Amount);
