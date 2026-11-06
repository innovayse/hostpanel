namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the <c>bank-transfer-rejected</c> email: the client's transfer was not accepted and the invoice is open again.</summary>
/// <param name="Invoice">The open invoice and the reference, read as <c>{{ invoice.* }}</c>.</param>
/// <param name="Note">The reason staff gave, read as <c>{{ note }}</c>.</param>
public sealed record BankTransferRejectedTemplateModel(BankTransferInvoiceTemplateData Invoice, string Note) : EmailTemplateModel;
