namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>An invoice as the card charge emails read it.</summary>
/// <param name="Id">The invoice id, read as <c>id</c>.</param>
/// <param name="Total">The invoice total, read as <c>total</c>.</param>
/// <param name="Currency">The ISO 4217 code the total is billed in, read as <c>currency</c>.</param>
/// <param name="Url">The absolute link to the invoice in the client area, read as <c>url</c>.</param>
public sealed record ChargedInvoiceTemplateData(int Id, decimal Total, string Currency, string Url);
