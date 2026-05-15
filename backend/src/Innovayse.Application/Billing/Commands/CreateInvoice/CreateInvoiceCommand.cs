namespace Innovayse.Application.Billing.Commands.CreateInvoice;

/// <summary>Command to create a new unpaid invoice for a client with at least one line item.</summary>
/// <param name="ClientId">FK to the client being invoiced.</param>
/// <param name="DueDate">Payment due date (UTC).</param>
/// <param name="Items">One or more line items to attach to the invoice.</param>
public record CreateInvoiceCommand(
    int ClientId,
    DateTimeOffset DueDate,
    IReadOnlyList<InvoiceItemRequest> Items);
