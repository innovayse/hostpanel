namespace Innovayse.Hostpanel.Application.Billing.Commands.RetryFulfilmentRecord;

/// <summary>Command to send a failed fulfilment record of an invoice again.</summary>
/// <param name="InvoiceId">The invoice the record belongs to; set from the route.</param>
/// <param name="RecordId">The record to retry; set from the route.</param>
public sealed record RetryFulfilmentRecordCommand(int InvoiceId, int RecordId);
