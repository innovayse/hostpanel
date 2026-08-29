namespace Innovayse.Application.Clients.Queries.GetClientSummary;

/// <summary>Invoice count and total for a single status.</summary>
/// <param name="Count">Number of invoices in this status.</param>
/// <param name="Total">Sum of totals for invoices in this status.</param>
public record InvoiceStatusCountDto(int Count, decimal Total);
