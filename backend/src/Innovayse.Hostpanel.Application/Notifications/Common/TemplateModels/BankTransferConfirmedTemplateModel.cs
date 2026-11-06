namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the <c>bank-transfer-confirmed</c> email: the client's transfer arrived and the invoice is paid.</summary>
/// <param name="Invoice">The paid invoice and the reference, read as <c>{{ invoice.* }}</c>.</param>
public sealed record BankTransferConfirmedTemplateModel(BankTransferInvoiceTemplateData Invoice) : EmailTemplateModel;
