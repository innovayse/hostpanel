namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the staff alert that a client chose to pay an invoice by bank transfer and it waits to be confirmed.</summary>
/// <param name="Invoice">The invoice, read as <c>{{ invoice.id }}</c>.</param>
/// <param name="Client">The owning client, read as <c>{{ client.id }}</c>.</param>
/// <param name="Reference">The reference the client was told to write on the transfer, read as <c>{{ reference }}</c>.</param>
/// <param name="AmountDue">What the transfer must be for, read as <c>{{ amount_due }}</c>.</param>
/// <param name="Currency">ISO 4217 code of the amount, read as <c>{{ currency }}</c>.</param>
public sealed record BankTransferRequestedAdminTemplateModel(
    InvoiceRefTemplateData Invoice,
    ClientRefTemplateData Client,
    string Reference,
    decimal AmountDue,
    string Currency) : EmailTemplateModel;
