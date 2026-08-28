namespace Innovayse.Application.Billing.Commands.PayMyInvoice;

using Innovayse.Application.Billing.Commands.PayInvoice;

/// <summary>Command for a client to pay one of their own invoices.</summary>
/// <remarks>
/// Carries no client id. Which account the invoice must belong to is resolved inside the handler
/// from the credential. The admin route that may settle any invoice dispatches
/// <see cref="PayInvoiceCommand"/> directly.
/// </remarks>
/// <param name="InvoiceId">The invoice to pay.</param>
/// <param name="Currency">
/// ISO 4217 currency code. Defaults to <see cref="PayInvoiceCommand.DefaultCurrency"/> -- the
/// same default the shared command carries, so an omitted field means the same thing on both.
/// </param>
public sealed record PayMyInvoiceCommand(
    int InvoiceId,
    string Currency = PayInvoiceCommand.DefaultCurrency);
