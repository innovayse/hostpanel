namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the <c>bank-transfer-details</c> email: how to pay the invoice the client chose to pay by bank transfer.</summary>
/// <param name="Invoice">The invoice and the reference, read as <c>{{ invoice.* }}</c>.</param>
/// <param name="Bank">The operator's bank details, read as <c>{{ bank.* }}</c>.</param>
public sealed record BankTransferDetailsTemplateModel(
    BankTransferInvoiceTemplateData Invoice, BankTransferBankTemplateData Bank) : EmailTemplateModel;
