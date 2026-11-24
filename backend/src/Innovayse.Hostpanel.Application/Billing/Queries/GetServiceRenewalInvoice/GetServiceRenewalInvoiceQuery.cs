namespace Innovayse.Hostpanel.Application.Billing.Queries.GetServiceRenewalInvoice;

/// <summary>
/// Finds the invoice that bills the period of a service starting at <paramref name="PeriodStart"/>: the newest
/// one that is not cancelled and carries a renewal line for that service and start.
/// </summary>
/// <param name="ServiceId">The service.</param>
/// <param name="PeriodStart">The start of the billed period.</param>
public sealed record GetServiceRenewalInvoiceQuery(int ServiceId, DateTimeOffset PeriodStart);
