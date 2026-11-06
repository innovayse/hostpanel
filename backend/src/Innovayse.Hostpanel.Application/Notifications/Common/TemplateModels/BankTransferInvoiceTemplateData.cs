namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>An invoice as the bank transfer emails read it.</summary>
/// <param name="Id">The invoice id, read as <c>id</c>.</param>
/// <param name="Total">What the transfer is for (the amount still due), read as <c>total</c>.</param>
/// <param name="Currency">The ISO 4217 code the amount is billed in, read as <c>currency</c>.</param>
/// <param name="Reference">The reference written on the transfer, read as <c>reference</c>.</param>
/// <param name="Url">The absolute link to the invoice in the client area, read as <c>url</c>.</param>
public sealed record BankTransferInvoiceTemplateData(int Id, decimal Total, string Currency, string Reference, string Url);
