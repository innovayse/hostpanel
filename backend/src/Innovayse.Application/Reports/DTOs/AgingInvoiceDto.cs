namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row of the Aging Invoices report.</summary>
public record AgingInvoiceDto(
    int InvoiceId,
    string Client,
    decimal Amount,
    string DueDate,
    int DaysOutstanding,
    string Bucket);
